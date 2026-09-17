using ChromeosUpdateEngine;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Helpers;
using FlasherCore.Firmware.Models;
using FlasherCore.Firmware.Types;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;
using System.Buffers;
using System.Buffers.Binary;
using ZstdSharp;

namespace FlasherCore.Firmware.Unpackers;

public class PayloadUnpacker : BaseUnpacker, IDisposable
{
    private const string PayloadHeaderMagic = "CrAU";
    private const ulong BrilloMajorPayloadVersion = 2;
    private const int BlockSize = 4096;
    private long _dataOffset;
    private PayloadEntry[] _entries;
    private SeekableZipStream _archive;
    public override int FileInfoCount => _entries?.Length ?? 0;
    public override UPFirmwareType FirmwareType => UPFirmwareType.Payload;

    public PayloadUnpacker(Stream source, uint bufferSize, bool leaveOpen = false)
        : base(source, bufferSize, leaveOpen)
    {
        _entries = Array.Empty<PayloadEntry>();
    }

    public override bool Parse()
    {
        try
        {
            _source.Seek(0, SeekOrigin.Begin);

            if (_source.IsZipStream())
            {
                _source = new SeekableZipStream(_source, "payload.bin",!_leaveOpen);
                
            }
            _source.Seek(0, SeekOrigin.Begin);
            Span<byte> headerBuf = stackalloc byte[24];
            _source.ReadExactly(headerBuf);

            if (
                headerBuf[0] != 'C'
                || headerBuf[1] != 'r'
                || headerBuf[2] != 'A'
                || headerBuf[3] != 'U'
            )
            {
                return false;
            }

            // 校验 Version (Big Endian)
            ulong version = BinaryPrimitives.ReadUInt64BigEndian(headerBuf.Slice(4, 8));
            if (version != BrilloMajorPayloadVersion)
            {
                return false;
            }

            // 获取 Manifest 长度
            ulong manifestLen = BinaryPrimitives.ReadUInt64BigEndian(headerBuf.Slice(12, 8));

            // 获取 Signature 长度
            uint metadataSignatureLen = BinaryPrimitives.ReadUInt32BigEndian(
                headerBuf.Slice(20, 4)
            );

            // 2. 计算 Payload 数据段的起始偏移
            // Header(24) + Manifest + Signature
            _dataOffset = 24 + (long)manifestLen + metadataSignatureLen;

            // 3. 读取并解析 Manifest (Protobuf)
            // Manifest 可能较大，分配一次堆内存是合理的
            byte[] manifestData = new byte[manifestLen];
            _source.ReadExactly(manifestData);

            var manifest = DeltaArchiveManifest.Parser.ParseFrom(manifestData);

            // 4. 构建内部文件列表
            var partitions = manifest.Partitions;
            _entries = new PayloadEntry[partitions.Count];

            for (int i = 0; i < partitions.Count; i++)
            {
                var p = partitions[i];
                _entries[i] = new PayloadEntry
                {
                    Name = p.PartitionName,
                    Size = p.NewPartitionInfo.Size,
                    Partition = p,
                };
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public override bool DumpToStream(Stream output, int id)
    {
        if (_entries == null || id < 0 || id >= _entries.Length)
            return false;

        var entry = _entries[id];
        var partition = entry.Partition;

        if (!output.CanSeek)
        {
            throw new InvalidOperationException(
                "Output stream must be seekable for Payload extraction."
            );
        }

        try
        {
            output.SetLength((long)entry.Size);
            var current = 0ul;
            var speedTracker = new SpeedTracker(_speed);
            foreach (var operation in partition.Operations)
            {
                if (operation.DstExtents == null || operation.DstExtents.Count == 0)
                    continue;

                long opDataOffset = _dataOffset + (long)operation.DataOffset;
                long opDataLength = (long)operation.DataLength;

                var subStream = new SubStream(_source, opDataOffset, opDataLength);
                var extent = operation.DstExtents[0];
                long outputOffset = (long)extent.StartBlock * BlockSize;
                ulong uoutputSize = extent.NumBlocks * BlockSize;
                long outputSize = (long)extent.NumBlocks * BlockSize;
                output.Seek(outputOffset, SeekOrigin.Begin);
                ProcessOperation(operation.Type, subStream, output, outputSize);
                current += uoutputSize;
                speedTracker.Update(uoutputSize);
                _progress?.Invoke(current, entry.Size);
            }
            _progress?.Invoke(entry.Size, entry.Size);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Dump Error: {ex.Message}");
            return false;
        }
    }

    private void ProcessOperation(
        InstallOperation.Types.Type type,
        Stream input,
        Stream output,
        long outputSize
    )
    {
        var bufferSize = (int)_bufferSize;
        switch (type)
        {
            case InstallOperation.Types.Type.Replace:
                input.CopyTo(output, bufferSize);
                break;

            case InstallOperation.Types.Type.ReplaceXz:
                using (var xz = new XZStream(input))
                {
                    xz.CopyTo(output, bufferSize);
                }
                break;

            case InstallOperation.Types.Type.ReplaceBz:
                using (
                    var bz = new BZip2Stream(
                        input,
                        SharpCompress.Compressors.CompressionMode.Decompress,
                        true
                    )
                )
                {
                    bz.CopyTo(output, bufferSize);
                }
                break;

            case InstallOperation.Types.Type.Zstd:
                using (var zstd = new DecompressionStream(input, bufferSize))
                {
                    zstd.CopyTo(output, bufferSize);
                }
                break;

            case InstallOperation.Types.Type.Zero:
                FillZero(output, outputSize);
                break;

            default:
                break;
        }
    }
    private void FillZero(Stream output, long length)
    {
        int bufSize = (int)_bufferSize;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufSize);
        try
        {
            Array.Clear(buffer, 0, bufSize);
            while (length > 0)
            {
                int toWrite = (int)Math.Min(length, bufSize);
                output.Write(buffer, 0, toWrite);
                length -= toWrite;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public override unsafe int GetEntries(Span<UPFirmwareEntry> entries)
    {
        if (_entries == null)
            return 0;

        int count = Math.Min(entries.Length, _entries.Length);

        fixed (UPFirmwareEntry* pDestBase = entries)
        {
            for (int i = 0; i < count; i++)
            {
                UPFirmwareEntry* pDest = pDestBase + i;
                var info = _entries[i];

                pDest->Index = (ushort)i;
                pDest->Start = 0;
                pDest->Length = info.Size;
                pDest->RealLength = info.Size;

                new Span<byte>(pDest->Name, 128).Clear();
                new Span<byte>(pDest->FileName, 128).Clear();

                if (!string.IsNullOrEmpty(info.Name))
                {
                    info.Name.CopyToPtr(pDest->Name, 128);
                    (info.Name + ".img").CopyToPtr(pDest->FileName, 128);
                }
            }
        }
        return _entries.Length;
    }
    public override bool OpenEntryStream(out Stream? output, ushort id)
    {
        output = null;
        if (_entries == null || id < 0 || id >= _entries.Length)
            return false;

        try
        {
            output = new PayloadPartitionStream(_source, _entries[id], _dataOffset, (int)_bufferSize);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to open Payload stream: {e.Message}");
            return false;
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _archive?.Dispose();
        }
        base.Dispose(disposing);
    }
}
