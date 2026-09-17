using FlasherCore.Common;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Models;
using FlasherCore.Firmware.Types;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SharpCompress.Archives.Zip;


namespace FlasherCore.Firmware.Unpackers;

public class OzipUnpacker : BaseUnpacker, IUnpacker<OzipHeader, OzipFileInfo>, IDisposable
{
    private static readonly byte[][] Keys =
 [
     new byte[] { 0xD6, 0xEE, 0xCF, 0x0A, 0xE5, 0xAC, 0xD4, 0xE0, 0xE9, 0xFE, 0x52, 0x2D, 0xE7, 0xCE, 0x38, 0x1E }, // mnkey
    new byte[] { 0xD6, 0xEC, 0xCF, 0x0A, 0xE5, 0xAC, 0xD4, 0xE0, 0xE9, 0x2E, 0x52, 0x2D, 0xE7, 0xC1, 0x38, 0x1E }, // mkey
    new byte[] { 0xD6, 0xDC, 0xCF, 0x0A, 0xD5, 0xAC, 0xD4, 0xE0, 0x29, 0x2E, 0x52, 0x2D, 0xB7, 0xC1, 0x38, 0x1E }, // realkey
    new byte[] { 0xD7, 0xDC, 0xCE, 0x1A, 0xD4, 0xAF, 0xDC, 0xE2, 0x39, 0x3E, 0x51, 0x61, 0xCB, 0xDC, 0x43, 0x21 }, // testkey
    new byte[] { 0xD7, 0xDB, 0xCE, 0x2A, 0xD4, 0xAD, 0xDC, 0xE1, 0x39, 0x3E, 0x55, 0x21, 0xCB, 0xDC, 0x43, 0x21 }, // utilkey
    new byte[] { 0xD7, 0xDB, 0xCE, 0x1A, 0xD4, 0xAF, 0xDC, 0xE1, 0x39, 0x3E, 0x51, 0x21, 0xCB, 0xDC, 0x43, 0x21 }, // R11s
    new byte[] { 0xD4, 0xD2, 0xCD, 0x61, 0xD4, 0xAF, 0xDC, 0xE1, 0x3B, 0x5E, 0x01, 0x22, 0x1B, 0xD1, 0x4D, 0x20 }, // FindX SDM845
    new byte[] { 0x26, 0x1C, 0xC7, 0x13, 0x1D, 0x7C, 0x14, 0x81, 0x29, 0x4E, 0x53, 0x2D, 0xB7, 0x52, 0x38, 0x1E }, // FindX
    new byte[] { 0x1C, 0xA2, 0x1E, 0x12, 0x27, 0x13, 0x35, 0xAE, 0x33, 0xAB, 0x81, 0xB2, 0xA7, 0xB1, 0x46, 0x22 }, // Realme 2 Pro
    new byte[] { 0xD4, 0xD2, 0xCE, 0x11, 0xD4, 0xAF, 0xDC, 0xE1, 0x3B, 0x3E, 0x01, 0x21, 0xCB, 0xD1, 0x4D, 0x20 }, // K1
    new byte[] { 0x1C, 0x4C, 0x1E, 0xA3, 0xA1, 0x25, 0x31, 0xAE, 0x49, 0x1B, 0x21, 0xBB, 0x31, 0x61, 0x3C, 0x11 }, // Realme 3 Pro
    new byte[] { 0x1C, 0x4C, 0x1E, 0xA3, 0xA1, 0x25, 0x31, 0xAE, 0x4A, 0x1B, 0x21, 0xBB, 0x31, 0xC1, 0x3C, 0x21 }, // Reno 10x
    new byte[] { 0x1C, 0x4A, 0x11, 0xA3, 0xA1, 0x25, 0x13, 0xAE, 0x44, 0x1B, 0x23, 0xBB, 0x31, 0x51, 0x31, 0x21 }, // Reno 2
    new byte[] { 0x1C, 0x4A, 0x11, 0xA3, 0xA1, 0x25, 0x89, 0xAE, 0x44, 0x1A, 0x23, 0xBB, 0x31, 0x51, 0x77, 0x33 }, // Realme X2
    new byte[] { 0x1C, 0x4A, 0x11, 0xA3, 0xA2, 0x25, 0x13, 0xAE, 0x54, 0x1B, 0x53, 0xBB, 0x31, 0x51, 0x31, 0x21 }, // Realme 5
    new byte[] { 0x24, 0x42, 0xCE, 0x82, 0x1A, 0x4F, 0x35, 0x2E, 0x33, 0xAE, 0x81, 0xB2, 0x2B, 0xC1, 0x46, 0x2E }, // R17 Pro
    new byte[] { 0x14, 0xC2, 0xCD, 0x62, 0x14, 0xCF, 0xDC, 0x27, 0x33, 0xAE, 0x81, 0xB2, 0x2B, 0xC1, 0x46, 0x2C }, // A3s
    new byte[] { 0x1E, 0x38, 0xC1, 0xB7, 0x2D, 0x52, 0x2E, 0x29, 0xE0, 0xD4, 0xAC, 0xD5, 0x0A, 0xCF, 0xDC, 0xD6 },
    new byte[] { 0x12, 0x34, 0x1E, 0xAA, 0xC4, 0xC1, 0x23, 0xCE, 0x19, 0x35, 0x56, 0xA1, 0xBB, 0xCC, 0x23, 0x2D },
    new byte[] { 0x21, 0x43, 0xDC, 0xCB, 0x21, 0x51, 0x3E, 0x39, 0xE1, 0xDC, 0xAF, 0xD4, 0x1A, 0xCE, 0xDB, 0xD7 },
    new byte[] { 0x2D, 0x23, 0xCC, 0xBB, 0xA1, 0x56, 0x35, 0x19, 0xCE, 0x23, 0xC1, 0xC4, 0xAA, 0x1E, 0x34, 0x12 }, // A77
    new byte[] { 0x17, 0x2B, 0x3E, 0x14, 0xE4, 0x6F, 0x3C, 0xE1, 0x3E, 0x2B, 0x51, 0x21, 0xCB, 0xDC, 0x43, 0x21 }, // Realme 1
    new byte[] { 0xAC, 0xAA, 0x1E, 0x12, 0xA7, 0x14, 0x31, 0xCE, 0x4A, 0x1B, 0x21, 0xBB, 0xA1, 0xC1, 0xC6, 0xA2 }, // Realme U1
    new byte[] { 0xAC, 0xAC, 0x1E, 0x13, 0xA7, 0x25, 0x31, 0xAE, 0x4A, 0x1B, 0x22, 0xBB, 0x31, 0xC1, 0xCC, 0x22 }, // Realme 3
    new byte[] { 0x1C, 0x44, 0x11, 0xA3, 0xA1, 0x25, 0x33, 0xAE, 0x44, 0x1B, 0x21, 0xBB, 0x31, 0x61, 0x3C, 0x11 }, // A1k
    new byte[] { 0x1C, 0x44, 0x16, 0xA8, 0xA4, 0x27, 0x17, 0xAE, 0x44, 0x15, 0x23, 0xB3, 0x36, 0x51, 0x31, 0x21 }, // Reno 3
    new byte[] { 0x55, 0xEE, 0xAA, 0x33, 0x11, 0x21, 0x33, 0xAE, 0x44, 0x1B, 0x23, 0xBB, 0x31, 0x51, 0x31, 0x21 }, // Reno Ace
    new byte[] { 0xAC, 0xAC, 0x1E, 0x13, 0xA1, 0x25, 0x31, 0xAE, 0x4A, 0x1B, 0x21, 0xBB, 0x31, 0xC1, 0x3C, 0x21 }, // Reno, K3
    new byte[] { 0xAC, 0xAC, 0x1E, 0x13, 0xA7, 0x24, 0x31, 0xAE, 0x4A, 0x1B, 0x22, 0xBB, 0xA1, 0xC1, 0xC6, 0xA2 }, // A9
    new byte[] { 0x12, 0xCA, 0xC1, 0x12, 0x11, 0xAA, 0xC3, 0xAE, 0xA2, 0x65, 0x86, 0x90, 0x12, 0x2C, 0x1E, 0x81 }, // A1
    new byte[] { 0x1C, 0xA2, 0x1E, 0x12, 0x27, 0x14, 0x35, 0xAE, 0x33, 0x1B, 0x81, 0xBB, 0xA7, 0xC1, 0x46, 0x12 }, // A5s
    new byte[] { 0xD1, 0xDA, 0xCF, 0x24, 0x35, 0x1C, 0xE4, 0x28, 0xA9, 0xCE, 0x32, 0xED, 0x87, 0x32, 0x32, 0x16 }, // Realme 1 (Res)
    new byte[] { 0xA1, 0xCC, 0x75, 0x11, 0x5C, 0xAE, 0xCB, 0x89, 0x0E, 0x4A, 0x56, 0x3C, 0xA1, 0xAC, 0x67, 0xC8 }, // A73
    new byte[] { 0x21, 0x32, 0x32, 0x1E, 0xA2, 0xCA, 0x86, 0x62, 0x1A, 0x11, 0x24, 0x1A, 0xBA, 0x51, 0x27, 0x22 }, // Realme 3 (Res)
    new byte[] { 0x22, 0xA2, 0x1E, 0x82, 0x17, 0x43, 0xE5, 0xEE, 0x33, 0xAE, 0x81, 0xB2, 0x27, 0xB1, 0x46, 0x2E }  // F3 Plus
 ];

    private static ReadOnlySpan<byte> OzipMagic => "OPPOENCRYPT!"u8;

    public override UPFirmwareType FirmwareType => UPFirmwareType.OZIP;
    public override int FileInfoCount => _fileInfos.Length;
    public OzipHeader Header => _header;
    public Span<OzipFileInfo> Entries => _fileInfos.AsSpan();

    private OzipHeader _header;
    private OzipFileInfo[] _fileInfos;

    private byte[]? _foundKey;
    private ZipArchive? _zipArchive;
    private List<ZipArchiveEntry>? _cachedZipEntries;

    public OzipUnpacker(Stream source, uint bufferSize,bool leaveOpen = false)
        : base(source, bufferSize, leaveOpen)
    {
        _fileInfos = [];
        _header = new OzipHeader();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _zipArchive?.Dispose();
            _cachedZipEntries = null;
        }
        base.Dispose(disposing);
    }

    public unsafe override bool Parse()
    {
        try
        {
            _source.Seek(0, SeekOrigin.Begin);

            // 1. Read Magic to Struct
            // 使用 stackalloc 避免分配
            Span<byte> magicBuffer = stackalloc byte[12];
            int read = _source.Read(magicBuffer);
            if (read < 12) return false;

            // 填充 Header 结构
            fixed (byte* pMagic = _header.Magic)
            {
                magicBuffer.CopyTo(new Span<byte>(pMagic, 12));
            }

            // 2. Determine Mode
            if (magicBuffer.SequenceEqual(OzipMagic))
            {
                // --- Mode 1: OZIP Payload ---
                _header.Mode = OzipMode.OzipPayload;
                _header.DataStartOffset = 0x1050; // Standard offset

                // Find Key
                _source.Seek(0x1050, SeekOrigin.Begin);
                Span<byte> testBlock = stackalloc byte[16];
                _source.ReadExactly(testBlock);

                _foundKey = FindKey(testBlock);
                if (_foundKey == null) return false;

                // Create Single File Info
                _fileInfos = new OzipFileInfo[1];
                _fileInfos[0] = CreateFileInfo(0, "Decrypted.zip", (ulong)_source.Length, OzipFileType.Payload, true);

                return true;
            }
            else if (magicBuffer[0] == 0x50 && magicBuffer[1] == 0x4B) // PK
            {
                // --- Mode 2: Zip Container ---
                _header.Mode = OzipMode.ZipContainer;

                _source.Seek(0, SeekOrigin.Begin);
                _zipArchive = ZipArchive.Open(_source,new SharpCompress.Readers.ReaderOptions()
                { BufferSize = (int)_bufferSize,LeaveStreamOpen = _leaveOpen });
                
                _cachedZipEntries = new List<ZipArchiveEntry>(_zipArchive.Entries);

                var infoList = new List<OzipFileInfo>(_cachedZipEntries.Count);
                int idx = 0;
                bool hasEncrypted = false;
                foreach (var entry in _cachedZipEntries)
                {
                    bool isEncrypted = false;
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        if (entry.Size >= 12)
                        {
                            Span<byte> head = stackalloc byte[12];
                            int r = ReadExactly(entryStream, head);
                            if (r == 12 && head.SequenceEqual(OzipMagic))
                            {
                                isEncrypted = true;

                                // 顺便尝试获取 Key (如果尚未获取)
                                if (_foundKey == null)
                                {
                                    // 结构: [Magic 12] [Padding 4] [Size 16] [Padding...] [Data at 0x50]
                                    // 我们需要跳过 0x50 字节 (Magic已读12，剩 0x50-12 = 68)
                                    // 然后读取 16 字节进行测试

                                    // 分配小 buffer 跳过数据 (entryStream 不一定支持 Seek)
                                    byte[] skipBuf = ArrayPool<byte>.Shared.Rent(68);
                                    try
                                    {
                                        ReadExactly(entryStream, skipBuf.AsSpan(0, 68));
                                        Span<byte> keyTestBuf = stackalloc byte[16];
                                        ReadExactly(entryStream, keyTestBuf);
                                        _foundKey = FindKey(keyTestBuf);
                                    }
                                    finally
                                    {
                                        ArrayPool<byte>.Shared.Return(skipBuf);
                                    }
                                }
                            }
                        }
                    }

                    infoList.Add(CreateFileInfo(
                        idx,
                        entry.Key,
                        (ulong)entry.Size,
                        isEncrypted ? OzipFileType.ZipEntryEncrypted : OzipFileType.ZipEntryPlain,
                        isEncrypted,
                        idx // ZipEntryIndex
                    ));
                    if (isEncrypted)
                        hasEncrypted = true;
                    idx++;
                }
                if (!hasEncrypted)
                    return false;
                _fileInfos = infoList.ToArray();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private unsafe OzipFileInfo CreateFileInfo(int index, string name, ulong size, OzipFileType type, bool isEnc, int zipIdx = -1)
    {
        var info = new OzipFileInfo
        {
            Index = index,
            Size = size,
            Type = type,
            IsEncrypted = isEnc,
            ZipEntryIndex = zipIdx
        };

        name.CopyToPtr(info.NameBuffer, 128);
        name.CopyToPtr(info.FileNameBuffer, 256);
        return info;
    }



    private byte[]? FindKey(ReadOnlySpan<byte> encryptedData)
    {
        if (encryptedData.Length < 16) return null;

        Span<byte> decBuffer = stackalloc byte[16];
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        foreach (var key in Keys)
        {
            aes.Key = key;
            // 尝试解密
            // .NET Standard 2.1+ supports Span-based TryDecrypt
            int bytesWritten = aes.DecryptEcb(encryptedData.Slice(0, 16), decBuffer, PaddingMode.None);
            if (bytesWritten > 0)
            {
                // 检查解密后的 Magic
                // PK.. (Zip), AVB0, ANDR
                if (decBuffer[0] == 0x50 && decBuffer[1] == 0x4B && decBuffer[2] == 0x03 && decBuffer[3] == 0x04) return key; // PK
                if (decBuffer[0] == 0x41 && decBuffer[1] == 0x56 && decBuffer[2] == 0x42 && decBuffer[3] == 0x30) return key; // AVB0
                if (decBuffer[0] == 0x41 && decBuffer[1] == 0x4E && decBuffer[2] == 0x44 && decBuffer[3] == 0x52) return key; // ANDR
            }
        }
        return null;
    }



    public override bool DumpToStream(Stream output, int id)
    {
        if (id < 0 || id >= _fileInfos.Length) return false;

        OzipFileInfo info = _fileInfos[id];

        try
        {
            switch (info.Type)
            {
                case OzipFileType.Payload: // Mode 1
                    return DumpMode1(output);

                case OzipFileType.ZipEntryPlain: // Mode 2 Plain
                    return DumpZipEntryPlain(info, output);

                case OzipFileType.ZipEntryEncrypted: // Mode 2 Encrypted
                    return DumpZipEntryEncrypted(info, output);
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    private bool DumpMode1(Stream output)
    {
        if (_foundKey == null) return false;

        _source.Seek(0x1050, SeekOrigin.Begin);
        long totalLen = _source.Length - 0x1050;

        var speedTracker = new SpeedTracker(s => _speed?.Invoke(s));

        using var aes = Aes.Create();
        aes.Key = _foundKey;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        // Pattern: [16 Enc] + [0x4000 Plain]
        int plainChunkSize = 0x4000;

        // RENT Buffers
        byte[] encBlock = ArrayPool<byte>.Shared.Rent(16);
        byte[] decBlock = ArrayPool<byte>.Shared.Rent(16);
        byte[] plainBuffer = ArrayPool<byte>.Shared.Rent(plainChunkSize);

        try
        {
            long processed = 0;
            while (true)
            {
                // 1. Read & Decrypt 16 bytes
                int r = _source.Read(encBlock, 0, 16);
                if (r == 0) break;

                // AES ECB decrypt
                // If r < 16, it's tail, usually implies plaintext or padding, just write it
                if (r == 16)
                {
                    aes.DecryptEcb(encBlock.AsSpan(0, 16), decBlock.AsSpan(0, 16), PaddingMode.None);
                    output.Write(decBlock, 0, 16);
                }
                else
                {
                    output.Write(encBlock, 0, r);
                }
                processed += r;

                // 2. Read & Copy 0x4000 Plain
                int toRead = plainChunkSize;
                int readPlainTotal = 0;

                while (readPlainTotal < toRead)
                {
                    int pr = _source.Read(plainBuffer, 0, toRead - readPlainTotal);
                    if (pr == 0) break;
                    output.Write(plainBuffer, 0, pr);
                    readPlainTotal += pr;
                }

                processed += readPlainTotal;
                speedTracker.Update((ulong)(r + readPlainTotal));
                _progress?.Invoke((ulong)processed, (ulong)totalLen);

                if (r < 16 || readPlainTotal < plainChunkSize) break; // EOF
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(encBlock);
            ArrayPool<byte>.Shared.Return(decBlock);
            ArrayPool<byte>.Shared.Return(plainBuffer);
        }

        return true;
    }

    private bool DumpZipEntryPlain(OzipFileInfo info, Stream output)
    {
        if (_cachedZipEntries == null || info.ZipEntryIndex < 0 || info.ZipEntryIndex >= _cachedZipEntries.Count) return false;

        var entry = _cachedZipEntries[info.ZipEntryIndex];
        using var zs = entry.OpenEntryStream();

        var speedTracker = new SpeedTracker(s => _speed?.Invoke(s));
        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)_bufferSize);

        try
        {
            int read;
            long total = 0;
            while ((read = zs.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                total += read;
                speedTracker.Update((ulong)read);
                _progress?.Invoke((ulong)total, (ulong)entry.Size);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return true;
    }

    private bool DumpZipEntryEncrypted(OzipFileInfo info, Stream output)
    {
        if (_foundKey == null || _cachedZipEntries == null) return false;
        var entry = _cachedZipEntries[info.ZipEntryIndex];

        using var zs = entry.OpenEntryStream(); // Non-seekable stream usually
        using var aes = Aes.Create();
        aes.Key = _foundKey;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        var speedTracker = new SpeedTracker(s => _speed?.Invoke(s));

        // Buffers
        // Chunk Header is 0x50 bytes
        // Payload block is 0x40000 (256KB)
        // Inside Payload: [16 Enc] + [0x3FF0 Plain] = 0x4000 (16KB)

        byte[] headerBuf = ArrayPool<byte>.Shared.Rent(0x50);
        byte[] encBlock = ArrayPool<byte>.Shared.Rent(16);
        byte[] decBlock = ArrayPool<byte>.Shared.Rent(16);
        byte[] plainChunk = ArrayPool<byte>.Shared.Rent(0x3FF0);

        try
        {
            long totalExtracted = 0;

            while (true)
            {
                // 1. Read Segment Header (0x50 bytes)
                int hRead = ReadExactly(zs, headerBuf.AsSpan(0, 0x50));
                if (hRead == 0) break; // EOF
                if (hRead < 0x50) break; // Partial header, unexpected EOF

                // Verify Magic (first 12 bytes)
                if (!new ReadOnlySpan<byte>(headerBuf, 0, 12).SequenceEqual(OzipMagic)) break;

                // Parse Size (String at offset 0x10, len 16)
                // Use Utf8Parser for zero-allocation parsing
                // string is like "12345\0\0..."
                var sizeSpan = new ReadOnlySpan<byte>(headerBuf, 0x10, 16);
                // Find actual length (index of \0)
                int numLen = sizeSpan.IndexOf((byte)0);
                if (numLen < 0) numLen = 16;

                if (!Utf8Parser.TryParse(sizeSpan.Slice(0, numLen), out long bdsize, out _)) break;

                // 2. Process Payload (Fixed Physical Size 0x40000)
                // We must read exactly 0x40000 from source, but write `bdsize` to output
                long bytesProcessedInBlock = 0;
                long bytesToWriteRemaining = bdsize;

                while (bytesProcessedInBlock < 0x40000)
                {
                    // a. Read 16 Encrypted
                    int rEnc = ReadExactly(zs, encBlock.AsSpan(0, 16));
                    bytesProcessedInBlock += rEnc;

                    if (rEnc < 16) break; // Stream ended

                    // Decrypt & Write
                    aes.DecryptEcb(encBlock.AsSpan(0, 16), decBlock.AsSpan(0, 16), PaddingMode.None);

                    if (bytesToWriteRemaining > 0)
                    {
                        int toWrite = (int)Math.Min(16, bytesToWriteRemaining);
                        output.Write(decBlock, 0, toWrite);
                        bytesToWriteRemaining -= toWrite;
                    }

                    // b. Read 0x3FF0 Plain
                    int rPlain = ReadExactly(zs, plainChunk.AsSpan(0, 0x3FF0));
                    bytesProcessedInBlock += rPlain;

                    if (bytesToWriteRemaining > 0)
                    {
                        int toWrite = (int)Math.Min(rPlain, bytesToWriteRemaining);
                        output.Write(plainChunk, 0, toWrite);
                        bytesToWriteRemaining -= toWrite;
                    }

                    if (bytesProcessedInBlock >= 0x40000) break;
                    if (rPlain < 0x3FF0) break; // Stream ended
                }

                totalExtracted += bdsize;
                speedTracker.Update((ulong)bdsize);
                _progress?.Invoke((ulong)totalExtracted, (ulong)entry.Size); // Note: Entry length is compressed size, approximation
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuf);
            ArrayPool<byte>.Shared.Return(encBlock);
            ArrayPool<byte>.Shared.Return(decBlock);
            ArrayPool<byte>.Shared.Return(plainChunk);
        }

        return true;
    }

    // Helper for reading from ZipStream (non-seekable)
    private int ReadExactly(Stream s, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int r = s.Read(buffer.Slice(total));
            if (r == 0) break;
            total += r;
        }
        return total;
    }

    public unsafe override int GetEntries(Span<UPFirmwareEntry> entries)
    {
        if (entries.Length < _fileInfos.Length) return -1;

        fixed (UPFirmwareEntry* pDestBase = entries)
        fixed (OzipFileInfo* pSourceBase = _fileInfos)
        {
            for (int i = 0; i < _fileInfos.Length; i++)
            {
                UPFirmwareEntry* pDest = pDestBase + i;
                OzipFileInfo* pSrc = pSourceBase + i;

                pDest->Index = (ushort)i;
                pDest->Start = 0; // Zip entries don't have linear offset in file
                pDest->Length = pSrc->Size;
                pDest->RealLength = pSrc->Size;

                // MemoryCopy from fixed buffers in OzipFileInfo to UPFirmwareEntry
                // Name (128 -> 32) (Truncate if needed)
                Buffer.MemoryCopy(pSrc->NameBuffer, pDest->Name, 32, 32); // Max 32 in UPFirmwareEntry?

                // FileName (256 -> 64)
                Buffer.MemoryCopy(pSrc->FileNameBuffer, pDest->FileName, 64, 64);
            }
        }
        return _fileInfos.Length;
    }
    public override bool OpenEntryStream(out Stream? output, ushort id)
    {
        output = null;
        if (id < 0 || id >= _fileInfos.Length) return false;

        var info = _fileInfos[id];

        try
        {
            if (_header.Mode == OzipMode.OzipPayload)
            {
                // Mode 1: 包装整个文件的解密流
                output = new OzipPayloadStream(_source, _foundKey!, (long)info.Size, _leaveOpen);
                return true;
            }
            else
            {
                // Mode 2: Zip 容器
                var entry = _cachedZipEntries![info.ZipEntryIndex];
                var baseEntryStream = entry.OpenEntryStream();

                if (info.IsEncrypted)
                {
                    // 包装 Zip Entry 内部的分块解密流
                    output = new OzipZipEntryStream(baseEntryStream, _foundKey!, (long)info.Size);
                }
                else
                {
                    output = baseEntryStream;
                }
                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to open OZIP stream: {e.Message}");
            return false;
        }
    }
}