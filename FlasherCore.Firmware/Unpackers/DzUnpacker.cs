using FlasherCore.Common;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Models;
using FlasherCore.Firmware.Types;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Archives.Zip;
using System.Runtime.InteropServices;
using SharpCompress.Compressors.Deflate;

namespace FlasherCore.Firmware.Unpackers;

public class DzUnpacker : BaseUnpacker, IUnpacker<DzHeader, DzChunkHeader>, IDisposable
{
    private DzHeader _header;
    private List<DzChunkHeader> _chunks;

    // 优化 1: 缓存 Chunk 数据的绝对偏移量，避免 Dump 时重复计算
    private List<long> _chunkOffsets;

    public DzUnpacker(Stream source, uint bufferSize,  bool leaveOpen = false)
        : base(source, bufferSize,  leaveOpen)
    {
        _chunks = new List<DzChunkHeader>();
        _chunkOffsets = new List<long>();
    }

    public DzHeader Header => _header;
    public Span<DzChunkHeader> Entries => CollectionsMarshal.AsSpan(_chunks);
    public override int FileInfoCount => _chunks.Count;
    public override UPFirmwareType FirmwareType => UPFirmwareType.DZ;

    public override unsafe bool Parse()
    {
        _source.Seek(0, SeekOrigin.Begin);

        int headerSize = sizeof(DzHeader);

        byte[] headerBuf = ArrayPool<byte>.Shared.Rent(headerSize);
        try
        {
            if (_source.Read(headerBuf, 0, headerSize) != headerSize) return false;
            _header = MemoryMarshal.Read<DzHeader>(headerBuf.AsSpan(0, headerSize));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuf);
        }

        if (!_header.IsValid())
        {
            return false;
        }

        long fileSize = _source.Length;
        int chunkHeaderSize = sizeof(DzChunkHeader);

        // 优化 3: 使用 ArrayPool 读取 Chunk Header
        byte[] chunkBuf = ArrayPool<byte>.Shared.Rent(chunkHeaderSize);
        try
        {
            Span<byte> chunkSpan = chunkBuf.AsSpan(0, chunkHeaderSize);

            // Header 后面紧接着就是第一个 Chunk Header
            long currentOffset = headerSize;

            while (currentOffset + chunkHeaderSize < fileSize)
            {
                _source.Seek(currentOffset, SeekOrigin.Begin);

                if (_source.Read(chunkBuf, 0, chunkHeaderSize) != chunkHeaderSize) break;

                DzChunkHeader chunk = MemoryMarshal.Read<DzChunkHeader>(chunkSpan);
                _chunks.Add(chunk);

                // 计算数据绝对起始位置：ChunkHeader 之后
                long dataStartOffset = currentOffset + chunkHeaderSize;
                _chunkOffsets.Add(dataStartOffset);

                // 计算下一个 Chunk Header 的位置 (当前数据起始 + 压缩数据长度)
                currentOffset = dataStartOffset + chunk.DataSize;

                if (currentOffset >= fileSize) break;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunkBuf);
        }

        return _chunks.Count > 0;
    }

    public override bool DumpToFile(string file, int id)
    {
        using var fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.Write);
        return DumpToStream(fs, id);
    }

    public override bool DumpToStream(Stream output, int id)
    {
        if (id < 0 || id >= _chunks.Count) return false;

        var chunk = _chunks[id];
        var speedTracker = new SpeedTracker(_speed);

        try
        {
            // 优化 4: 直接从预计算的列表中获取偏移，O(1) 复杂度
            long dataOffset = _chunkOffsets[id];

            // 无法避免 SubStream 的分配，因为 ZLibStream 需要隔离流
            // 但 SubStream 本身只是一个轻量级 Wrapper
            using (var subStream = new SubStream(_source, dataOffset, chunk.DataSize))
            using (var zlib = new ZlibStream(subStream,SharpCompress.Compressors.CompressionMode.Decompress))
            {
                // 优化 5: 使用 ArrayPool 进行解压数据缓冲
                int bufSize = (int)_bufferSize;
                byte[] poolBuffer = ArrayPool<byte>.Shared.Rent(bufSize);

                try
                {
                    ulong totalRead = 0;
                    while (true)
                    {
                        int read = zlib.Read(poolBuffer, 0, bufSize);
                        if (read == 0) break;

                        output.Write(poolBuffer, 0, read);

                        totalRead += (ulong)read;
                        speedTracker.Update(read);

                        // 注意：TargetSize 是解压后的大小
                        _progress?.Invoke(totalRead, chunk.TargetSize);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(poolBuffer);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting chunk {id}: {ex.Message}");
            return false;
        }

        return true;
    }

    public override unsafe int GetEntries(Span<UPFirmwareEntry> entries)
    {
        if (entries.Length < _chunks.Count) return -1;

        var spanSrc = CollectionsMarshal.AsSpan(_chunks);
        var offsetSpan = CollectionsMarshal.AsSpan(_chunkOffsets);

        for (int i = 0; i < spanSrc.Length; i++)
        {
            ref var src = ref spanSrc[i];
            ref var dest = ref entries[i];

            dest.Index = (ushort)i;
            // 填充真实的物理偏移 (Optional, 如果不需要显示可以填0)
            dest.Start = (ulong)offsetSpan[i];

            dest.Length = src.DataSize;   // Compressed
            dest.RealLength = src.TargetSize; // Uncompressed

            fixed (byte* pChunkName = src.ChunkName)
            fixed (byte* pSliceName = src.SliceName)
            fixed (byte* pDestName = dest.Name)
            fixed (byte* pDestFileName = dest.FileName)
            {
                Buffer.MemoryCopy(pChunkName, pDestFileName, 128, 64);
                Buffer.MemoryCopy(pSliceName, pDestName, 128, 32);
            }
        }
        return _chunks.Count;
    }
    public override bool OpenEntryStream(out Stream? output, ushort id)
    {
        output = null;
        if (id < 0 || id >= _chunks.Count) return false;

        var chunk = _chunks[id];
        long dataOffset = _chunkOffsets[id];

        try
        {
            var subStream = new SubStream(_source, dataOffset, chunk.DataSize);
           output = new ZlibStream(subStream, SharpCompress.Compressors.CompressionMode.Decompress);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to open DZ chunk stream: {e.Message}");
            return false;
        }
    }
}