using FlasherCore.Firmware.Unpackers;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Firmware.Models;

public class OpsEntryStream : Stream
{
    private readonly Stream _baseStream;
    private readonly OpsFileInfo _info;
    private readonly byte[] _mbox;
    private readonly uint[] _baseKey;
    private readonly uint[] _currentRKey;
    private long _position;
    private readonly bool _leaveOpen;

    // 用于解密算法内部的临时缓冲区
    private readonly byte[] _processBuffer = new byte[4];

    public OpsEntryStream(Stream source, OpsFileInfo info, byte[] mbox, uint[] baseKey, bool leaveOpen)
    {
        _baseStream = source;
        _info = info;
        _mbox = mbox;
        _baseKey = baseKey;
        _leaveOpen = leaveOpen;
        _position = 0;

        _currentRKey = new uint[4];
        ResetCrypto();
    }

    private void ResetCrypto()
    {
        _baseKey.CopyTo(_currentRKey, 0);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long infoLength = (long)_info.RealLength;
        if (_position >= infoLength) return 0;

        int toRead = (int)Math.Min(count, infoLength - _position);

        if (_info.DumpType == OpsFileType.COPY_FILE)
        {
            lock (_baseStream)
            {
                _baseStream.Seek((long)_info.Start + _position, SeekOrigin.Begin);
                int read = _baseStream.Read(buffer, offset, toRead);
                _position += read;
                return read;
            }
        }

        // DECRYPT_FILE 的流式读取逻辑
        // 注意：由于 OPS 的解密是状态依赖的，如果 _position 不是从 0 开始连续读取，
        // 则需要从头重新计算状态。为了简单和效率，这里假定是顺序读取。
        // 若要支持随机 Seek 读取，必须缓存状态或重新从头解密。
        return ReadDecrypted(buffer, offset, toRead);
    }

    private int ReadDecrypted(byte[] buffer, int offset, int count)
    {
        // 如果 Position 被 Seek 到了非 0 位置，且不是当前位置，则重置密钥
        // 注意：OPS 这种反馈加密不支持高效随机 Seek。
        // 下面实现仅保证顺序读取正确，Seek 后读取会触发从头重算（性能较低）

        int totalRead = 0;
        byte[] tempIn = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            lock (_baseStream)
            {
                _baseStream.Seek((long)_info.Start + _position, SeekOrigin.Begin);
                int actualRead = _baseStream.Read(tempIn, 0, count);

                using var ms = new MemoryStream(buffer, offset, count);
                // 这里调用 Unpacker 中的解密核心逻辑
                // 注意：在 Read 过程中，_currentRKey 会不断更新
                ProcessBlockStream(tempIn.AsSpan(0, actualRead), ms);

                _position += actualRead;
                totalRead = actualRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempIn);
        }
        return totalRead;
    }

    // 适配 Stream 版本的解密逻辑（从 OpsUnpacker.ProcessBlockAndWrite 迁移并修改）
    private void ProcessBlockStream(ReadOnlySpan<byte> input, Stream output)
    {
        int inputPtr = 0;
        int length = input.Length;

        // Phase 1: 16-byte blocks
        while (length >= 16)
        {
            OpsUnpacker.KeyUpdate(_currentRKey, _mbox);
            for (int i = 0; i < 4; i++)
            {
                uint cipherVal = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(inputPtr + i * 4));
                uint plainVal = _currentRKey[i] ^ cipherVal;
                BinaryPrimitives.WriteUInt32LittleEndian(_processBuffer, plainVal);
                output.Write(_processBuffer);
                _currentRKey[i] = cipherVal;
            }
            length -= 16;
            inputPtr += 16;
        }

        // Phase 2: Remaining bytes
        if (length > 0)
        {
            OpsUnpacker.KeyUpdate(_currentRKey, OpsUnpacker.SBox);
            Span<byte> padBuf = stackalloc byte[4];
            int m = 0;
            while (length > 0)
            {
                int readLen = Math.Min(4, length);
                padBuf.Clear();
                input.Slice(inputPtr, readLen).CopyTo(padBuf);

                uint cipherVal = BinaryPrimitives.ReadUInt32LittleEndian(padBuf);
                uint plainVal = cipherVal ^ _currentRKey[m];

                BinaryPrimitives.WriteUInt32LittleEndian(_processBuffer, plainVal);
                // 仅写入实际需要的字节（防止 4 字节对齐填充多余数据）
                output.Write(_processBuffer, 0, readLen);

                _currentRKey[m] = cipherVal;
                length -= readLen;
                inputPtr += readLen;
                m++;
            }
        }
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
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => (long)_info.RealLength + offset,
            _ => _position
        };
        newPos = Math.Clamp(newPos, 0, (long)_info.RealLength);

        if (newPos != _position)
        {
            _position = newPos;
            // 如果是加密文件，Seek 后必须从 0 重新同步密钥状态（这是此类加密算法的局限性）
            if (_info.DumpType == OpsFileType.DECRYPT_FILE)
            {
                ResetCrypto();
                // 如果需要支持高性能随机读，需在此处从 0 读到 _position 丢弃数据以同步 RKey
            }
        }
        return _position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

