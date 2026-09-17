using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FlasherCore.Firmware.Models;

public class MtkEntryStream : Stream
{
    private readonly Stream _baseStream;
    private readonly OfpMtkFileInfo _info;
    private readonly byte[] _decryptedHeader;
    private long _position;
    private readonly bool _leaveOpen;

    public MtkEntryStream(Stream source, Aes aes, byte[] iv, OfpMtkFileInfo info, bool leaveOpen)
    {
        _baseStream = source;
        _info = info;
        _leaveOpen = leaveOpen;
        _position = 0;

        if (info.EncLength > 0)
        {
            // 修复运算二义性：强制转换为 long
            var alignedEncLength = (int)(((info.EncLength + 15) / 16) * 16);
            Span<byte> encData = new byte[alignedEncLength];
            _decryptedHeader = new byte[alignedEncLength];

            lock (_baseStream)
            {
                _baseStream.Seek((long)info.Start, SeekOrigin.Begin);
                _baseStream.ReadExactly(encData.Slice(0, (int)info.EncLength));
            }

            // 假设使用的是加密扩展方法
            aes.TryDecryptCfb(encData, iv, _decryptedHeader, out _, feedbackSizeInBits: 128);
        }
        else
        {
            _decryptedHeader = Array.Empty<byte>();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // 修复 CS0034/CS0266: 将 ulong 显式转换为 long 进行比较和运算
        long infoLength = (long)_info.Length;
        long infoEncLength = (long)_info.EncLength;

        if (_position >= infoLength) return 0;

        int totalRead = 0;
        int remainingInEntry = (int)(infoLength - _position);
        int toRead = Math.Min(count, remainingInEntry);

        // 1. 处理已解密的头部区域
        if (_position < infoEncLength)
        {
            int headerAvailable = (int)(infoEncLength - _position);
            int copyCount = Math.Min(toRead, headerAvailable);

            Buffer.BlockCopy(_decryptedHeader, (int)_position, buffer, offset, copyCount);

            _position += copyCount;
            totalRead += copyCount;
            offset += copyCount;
            toRead -= copyCount;
        }

        // 2. 处理原始数据区域
        if (toRead > 0)
        {
            lock (_baseStream)
            {
                // 修复 CS0034: 显式转换运算
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
    public override long Length => (long)_info.Length; // 修复隐式转换错误
    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long infoLength = (long)_info.Length;
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => infoLength + offset,
            _ => _position
        };
        // 修复 CS0121: 明确指定参数类型以消除 Clamp 二义性
        _position = Math.Clamp(newPos, (long)0, infoLength);
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            // 如果需要关闭原流，则在这里处理
            // 通常由 BaseUnpacker 管理生命周期，这里可能不需要做任何事
        }
        base.Dispose(disposing);
    }
}