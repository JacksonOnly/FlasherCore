namespace FlasherCore.Common.Utilities;

public class SubStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _startPosition;
    private readonly long _length;
    private long _position;

    public SubStream(Stream baseStream, long offset, long length)
    {
        _baseStream = baseStream;
        _startPosition = offset;
        _length = length;
        _position = 0;
        if (_baseStream.CanSeek)
        {
            _baseStream.Seek(_startPosition, SeekOrigin.Begin);
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => false; // 简化实现，解压流通常顺序读
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length) return 0;

        int toRead = (int)Math.Min(count, _length - _position);
        int read = _baseStream.Read(buffer, offset, toRead);
        _position += read;
        return read;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}