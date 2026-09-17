using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FlasherCore.Firmware.Models;

public class OzipZipEntryStream : Stream
{
    private readonly Stream _zipStream;
    private readonly Aes _aes;
    private readonly long _totalSize;
    private long _position;

    private byte[]? _currentBlockData;
    private int _currentBlockOffset;
    private int _currentBlockLength;

    public OzipZipEntryStream(Stream zipStream, byte[] key, long totalSize)
    {
        _zipStream = zipStream;
        _totalSize = totalSize;
        _aes = Aes.Create();
        _aes.Key = key;
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_currentBlockData == null || _currentBlockOffset >= _currentBlockLength)
        {
            if (!TryReadNextBlock()) return 0;
        }

        int available = _currentBlockLength - _currentBlockOffset;
        int toCopy = Math.Min(count, available);
        Buffer.BlockCopy(_currentBlockData!, _currentBlockOffset, buffer, offset, toCopy);

        _currentBlockOffset += toCopy;
        _position += toCopy;
        return toCopy;
    }

    private bool TryReadNextBlock()
    {
        Span<byte> header = stackalloc byte[0x50];
        int r = ReadExactlyInternal(_zipStream, header);
        if (r < 0x50) return false;

        // 解析当前块的实际有效负载大小
        var sizeSpan = header.Slice(0x10, 16);
        int numLen = sizeSpan.IndexOf((byte)0);
        if (!Utf8Parser.TryParse(sizeSpan.Slice(0, numLen == -1 ? 16 : numLen), out long bdSize, out _)) return false;

        _currentBlockData = new byte[bdSize];
        _currentBlockOffset = 0;
        _currentBlockLength = (int)bdSize;

        // 物理上块大小固定为 0x40000
        int physicalRead = 0;
        int writePtr = 0;
        Span<byte> enc = stackalloc byte[16];
        Span<byte> dec = stackalloc byte[16];

        while (physicalRead < 0x40000)
        {
            // 解密每 16 字节
            int rEnc = ReadExactlyInternal(_zipStream, enc);
            physicalRead += rEnc;
            if (rEnc < 16) break;

            _aes.DecryptEcb(enc, dec, PaddingMode.None);
            int toCopyEnc = Math.Min(16, _currentBlockLength - writePtr);
            if (toCopyEnc > 0)
            {
                dec.Slice(0, toCopyEnc).CopyTo(_currentBlockData.AsSpan(writePtr));
                writePtr += toCopyEnc;
            }

            // 读取 0x3FF0 明文
            int toReadPlain = Math.Min(0x3FF0, 0x40000 - physicalRead);
            byte[] plainBuf = new byte[toReadPlain];
            int rPlain = ReadExactlyInternal(_zipStream, plainBuf);
            physicalRead += rPlain;

            int toCopyPlain = Math.Min(rPlain, _currentBlockLength - writePtr);
            if (toCopyPlain > 0)
            {
                Buffer.BlockCopy(plainBuf, 0, _currentBlockData, writePtr, toCopyPlain);
                writePtr += toCopyPlain;
            }
            if (rPlain < toReadPlain) break;
        }
        return true;
    }

    private int ReadExactlyInternal(Stream s, Span<byte> buf)
    {
        int total = 0;
        while (total < buf.Length)
        {
            int r = s.Read(buf.Slice(total));
            if (r == 0) break;
            total += r;
        }
        return total;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _totalSize; // 注意：这只是估算值，因为 Zip Entry 大小不固定
    public override long Position { get => _position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) _aes.Dispose(); base.Dispose(disposing); }
}