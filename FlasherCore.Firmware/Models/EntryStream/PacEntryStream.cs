using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Firmware.Models;

public class PacEntryStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _baseOffset;
    private readonly long _length;
    private long _position;
    private readonly bool _leaveOpen;

    public PacEntryStream(Stream source, long offset, long length, bool leaveOpen)
    {
        _baseStream = source;
        _baseOffset = offset;
        _length = length;
        _leaveOpen = leaveOpen;
        _position = 0;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length) return 0;

        int toRead = (int)Math.Min(count, _length - _position);
        int read;

        lock (_baseStream)
        {
            _baseStream.Seek(_baseOffset + _position, SeekOrigin.Begin);
            read = _baseStream.Read(buffer, offset, toRead);
        }

        _position += read;
        return read;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => _position
        };

        _position = Math.Clamp(newPos, 0, _length);
        return _position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            // 注意：通常基础流由 BaseUnpacker 统一管理
            // 如果需要子流关闭时同时关闭原流，取消下面注释
            // _baseStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
