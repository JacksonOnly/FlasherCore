using ChromeosUpdateEngine;
using FlasherCore.Common.Utilities;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;
using System;
using System.Collections.Generic;
using System.Text;
using ZstdSharp;

namespace FlasherCore.Firmware.Models;

public class PayloadPartitionStream : Stream
{
    private readonly Stream _source;
    private readonly PayloadEntry _entry;
    private readonly long _dataOffset;
    private readonly int _bufferSize;
    private long _position;

    // 内部缓存：因为一个 Operation 可能很大，我们需要缓存当前正在处理的操作解压后的数据
    private MemoryStream? _currentOpBuffer;
    private int _currentOpIndex = -1;
    private const int BlockSize = 4096;

    public PayloadPartitionStream(Stream source, PayloadEntry entry, long dataOffset, int bufferSize)
    {
        _source = source;
        _entry = entry;
        _dataOffset = dataOffset;
        _bufferSize = bufferSize;
        _position = 0;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= (long)_entry.Size) return 0;

        int totalRead = 0;
        while (totalRead < count && _position < (long)_entry.Size)
        {
            // 1. 确定当前 Position 属于哪个 Operation
            int opIndex = FindOperationIndexForPosition(_position, out long opStartPos);
            if (opIndex == -1) break;

            // 2. 如果切换了操作块，或者缓存为空，则加载该块
            if (opIndex != _currentOpIndex)
            {
                LoadOperationToBuffer(opIndex);
                _currentOpIndex = opIndex;
            }

            // 3. 从缓存读取数据
            _currentOpBuffer!.Seek(_position - opStartPos, SeekOrigin.Begin);
            int available = (int)(_currentOpBuffer.Length - _currentOpBuffer.Position);
            int toCopy = Math.Min(count - totalRead, available);

            int read = _currentOpBuffer.Read(buffer, offset + totalRead, toCopy);
            _position += read;
            totalRead += read;
        }

        return totalRead;
    }

    private int FindOperationIndexForPosition(long pos, out long opStartPos)
    {
        long currentOffset = 0;
        var ops = _entry.Partition.Operations;
        for (int i = 0; i < ops.Count; i++)
        {
            long opSize = (long)ops[i].DstExtents[0].NumBlocks * BlockSize;
            if (pos >= currentOffset && pos < currentOffset + opSize)
            {
                opStartPos = currentOffset;
                return i;
            }
            currentOffset += opSize;
        }
        opStartPos = 0;
        return -1;
    }

    private void LoadOperationToBuffer(int index)
    {
        _currentOpBuffer?.Dispose();
        var op = _entry.Partition.Operations[index];
        long opDataLength = (long)op.DataLength;
        long opDataOffset = _dataOffset + (long)op.DataOffset;
        long outputSize = (long)op.DstExtents[0].NumBlocks * BlockSize;

        _currentOpBuffer = new MemoryStream(new byte[outputSize]);

        using var sub = new SubStream(_source, opDataOffset, opDataLength);

        // 复用原有的 ProcessOperation 逻辑 (需确保该方法可见或逻辑一致)
        ProcessOperationInline(op.Type, sub, _currentOpBuffer, outputSize);
        _currentOpBuffer.Position = 0;
    }

    // 内部解压逻辑适配
    private void ProcessOperationInline(InstallOperation.Types.Type type, Stream input, Stream output, long outSize)
    {
        switch (type)
        {
            case InstallOperation.Types.Type.Replace: input.CopyTo(output); break;
            case InstallOperation.Types.Type.ReplaceXz:
                using (var xz = new XZStream(input)) xz.CopyTo(output); break;
            case InstallOperation.Types.Type.ReplaceBz:
                using (var bz = new BZip2Stream(input, SharpCompress.Compressors.CompressionMode.Decompress, true))
                    bz.CopyTo(output); break;
            case InstallOperation.Types.Type.Zstd:
                using (var zstd = new DecompressionStream(input)) zstd.CopyTo(output); break;
            case InstallOperation.Types.Type.Zero:
                output.SetLength(outSize); break; // MemoryStream 默认是 0
            default: break;
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => (long)_entry.Size;
    public override long Position { get => _position; set => _position = value; }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => (long)_entry.Size + offset,
            _ => _position
        };
        _position = Math.Clamp(newPos, 0, (long)_entry.Size);
        return _position;
    }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing) _currentOpBuffer?.Dispose();
        base.Dispose(disposing);
    }
}