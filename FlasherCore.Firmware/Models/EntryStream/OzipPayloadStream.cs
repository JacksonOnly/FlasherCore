using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FlasherCore.Firmware.Models;

public class OzipPayloadStream : Stream
{
    private readonly Stream _baseStream;
    private readonly byte[] _key;
    private readonly long _length;
    private readonly bool _leaveOpen;
    private long _position;
    private readonly Aes _aes;

    public OzipPayloadStream(Stream source, byte[] key, long length, bool leaveOpen)
    {
        _baseStream = source;
        _key = key;
        _length = length;
        _leaveOpen = leaveOpen;
        _aes = Aes.Create();
        _aes.Key = _key;
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;

        _baseStream.Seek(0x1050, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        Span<byte> encBlock = stackalloc byte[16];
        Span<byte> decBlock = stackalloc byte[16];

        while (totalRead < count)
        {
            long currentFilePos = _position + 0x1050;
            // 计算当前在哪个 0x4010 周期内
            int offsetInCycle = (int)(_position % 0x4010);
            int toRead = Math.Min(count - totalRead, 0x4010 - offsetInCycle);

            if (offsetInCycle < 16) // 在加密的 16 字节内
            {
                int subToRead = Math.Min(toRead, 16 - offsetInCycle);
                lock (_baseStream)
                {
                    _baseStream.Seek(currentFilePos - offsetInCycle, SeekOrigin.Begin);
                    _baseStream.ReadExactly(encBlock);
                }
                _aes.DecryptEcb(encBlock, decBlock, PaddingMode.None);
                decBlock.Slice(offsetInCycle, subToRead).CopyTo(buffer.AsSpan(offset + totalRead));

                totalRead += subToRead;
                _position += subToRead;
            }
            else // 在明文数据内
            {
                int subToRead = toRead;
                lock (_baseStream)
                {
                    _baseStream.Seek(currentFilePos, SeekOrigin.Begin);
                    int r = _baseStream.Read(buffer, offset + totalRead, subToRead);
                    totalRead += r;
                    _position += r;
                    if (r == 0) break;
                }
            }
        }
        return totalRead;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length - 0x1050;
    public override long Position { get => _position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) _aes.Dispose(); base.Dispose(disposing); }
}