using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FlasherCore.Firmware.Models;

public class QcEntryStream : Stream
{
    private readonly Stream _baseStream;
    private readonly OfpQcFileInfo _info;
    private readonly byte[]? _decryptedHeader;
    private readonly long _headerSize;
    private long _position;
    private readonly bool _leaveOpen;

    public QcEntryStream(Stream source, Aes? aes, byte[]? iv, OfpQcFileInfo info, bool leaveOpen)
    {
        _baseStream = source;
        _info = info;
        _leaveOpen = leaveOpen;
        _position = 0;

        // 高通特定的解密逻辑：仅针对 DECRYPT_FILE 类型处理开头块
        if (info.DumpType == OfpQcFileType.DECRYPT_FILE && aes != null && iv != null)
        {
            const long decryptBlockSize = 0x40000;
            _headerSize = Math.Min(decryptBlockSize, (long)info.RealLength);

            // AES-CFB 需要 16 字节对齐
            long readSizeAligned = _headerSize;
            if (readSizeAligned % 16 != 0)
                readSizeAligned += (16 - (readSizeAligned % 16));

            byte[] encBuffer = new byte[readSizeAligned];
            _decryptedHeader = new byte[readSizeAligned];

            lock (_baseStream)
            {
                _baseStream.Seek((long)info.Start, SeekOrigin.Begin);
                _baseStream.ReadExactly(encBuffer.AsSpan(0, (int)_headerSize));
            }

            aes.TryDecryptCfb(encBuffer, iv, _decryptedHeader, out _, feedbackSizeInBits: 128);
        }
        else
        {
            _headerSize = 0;
            _decryptedHeader = null;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long infoLength = (long)_info.RealLength;
        if (_position >= infoLength) return 0;

        int totalRead = 0;
        int remainingInEntry = (int)(infoLength - _position);
        int toRead = Math.Min(count, remainingInEntry);

        // 1. 读取解密后的头部块
        if (_position < _headerSize && _decryptedHeader != null)
        {
            int headerAvailable = (int)(_headerSize - _position);
            int copyCount = Math.Min(toRead, headerAvailable);

            Buffer.BlockCopy(_decryptedHeader, (int)_position, buffer, offset, copyCount);

            _position += copyCount;
            totalRead += copyCount;
            offset += copyCount;
            toRead -= copyCount;
        }

        // 2. 读取剩余的原始数据
        if (toRead > 0)
        {
            lock (_baseStream)
            {
                _baseStream.Seek((long)_info.Start + _position, SeekOrigin.Begin);
                int readFromStream = _baseStream.Read(buffer, offset, toRead);
                _position += readFromStream;
                totalRead += readFromStream;
            }
        }

        return totalRead;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => (long)_info.RealLength;
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long infoLength = (long)_info.RealLength;
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => infoLength + offset,
            _ => _position
        };
        _position = Math.Clamp(newPos, (long)0, infoLength);
        return _position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
        }
        base.Dispose(disposing);
    }
}