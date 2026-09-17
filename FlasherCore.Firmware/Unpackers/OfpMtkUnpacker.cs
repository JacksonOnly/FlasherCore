using System;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Runtime.InteropServices;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Types;
using System.Text;
using FlasherCore.Common;
using FlasherCore.Firmware.Models;

namespace FlasherCore.Firmware.Unpackers;

public class OfpMtkUnpacker : BaseUnpacker, IUnpacker<OfpMtkHeader, OfpMtkFileInfo>, IDisposable
{
    private static readonly byte[][][] KeyTables =
    [
        [
            [0x67, 0x65, 0x79, 0x63, 0x78, 0x75, 0x65, 0xE8, 0x37, 0xD2, 0x26, 0xB6, 0x9A, 0x49, 0x5D, 0x21],
            [0xF6, 0xC5, 0x02, 0x03, 0x51, 0x5A, 0x2C, 0xE7, 0xD8, 0xC3, 0xE1, 0xF9, 0x38, 0xB7, 0xE9, 0x4C],
            [0x42, 0xF2, 0xD5, 0x39, 0x91, 0x37, 0xE2, 0xB2, 0x81, 0x3C, 0xD8, 0xEC, 0xDF, 0x2F, 0x4D, 0x72],
        ],
        [
            [0x9E, 0x4F, 0x32, 0x63, 0x9D, 0x21, 0x35, 0x7D, 0x37, 0xD2, 0x26, 0xB6, 0x9A, 0x49, 0x5D, 0x21],
            [0xA3, 0xD8, 0xD3, 0x58, 0xE4, 0x2F, 0x5A, 0x9E, 0x93, 0x1D, 0xD3, 0x91, 0x7D, 0x9A, 0x32, 0x18],
            [0x38, 0x69, 0x35, 0x39, 0x91, 0x37, 0x41, 0x6B, 0x67, 0x41, 0x6B, 0xEC, 0xF2, 0x2F, 0x51, 0x9A],
        ],
        [
            [0x89, 0x2D, 0x57, 0xE9, 0x2A, 0x4D, 0x8A, 0x97, 0x5E, 0x3C, 0x21, 0x6B, 0x7C, 0x9D, 0xE1, 0x89],
            [0xD2, 0x6D, 0xF2, 0xD9, 0x91, 0x37, 0x85, 0xB1, 0x45, 0xD1, 0x8C, 0x72, 0x19, 0xB8, 0x9F, 0x26],
            [0x51, 0x69, 0x89, 0xE4, 0xA1, 0xBF, 0xC7, 0x8B, 0x36, 0x5C, 0x6B, 0xC5, 0x7D, 0x94, 0x43, 0x91],
        ],
        [
            [0x27, 0x82, 0x79, 0x63, 0x78, 0x72, 0x65, 0xEF, 0x89, 0xD1, 0x26, 0xB6, 0x9A, 0x49, 0x5A, 0x21],
            [0x82, 0xC5, 0x02, 0x03, 0x28, 0x5A, 0x2C, 0xE7, 0xD8, 0xC3, 0xE1, 0x98, 0x38, 0x3C, 0xE9, 0x4C],
            [0x42, 0x2D, 0xD5, 0x39, 0x91, 0x81, 0xE2, 0x23, 0x81, 0x3C, 0xD8, 0xEC, 0xDF, 0x2E, 0x4D, 0x72],
        ],
        [
            [0x3C, 0x4A, 0x61, 0x8D, 0x9B, 0xF2, 0xE4, 0x27, 0x9D, 0xC7, 0x58, 0xCD, 0x53, 0x51, 0x47, 0xC3],
            [0x87, 0xB1, 0x3D, 0x29, 0x70, 0x9A, 0xC1, 0xBF, 0x23, 0x82, 0x27, 0x6C, 0x4E, 0x8D, 0xF2, 0x32],
            [0x59, 0xB7, 0xA8, 0xE9, 0x67, 0x26, 0x5E, 0x9B, 0xCA, 0xBE, 0x24, 0x69, 0xFE, 0x4A, 0x91, 0x5E],
        ],
        [
            [0x1C, 0x32, 0x88, 0x82, 0x2B, 0xF8, 0x24, 0x25, 0x9D, 0xC8, 0x52, 0xC1, 0x73, 0x31, 0x27, 0xD3],
            [0xE7, 0x91, 0x8D, 0x22, 0x79, 0x91, 0x81, 0xCF, 0x23, 0x12, 0x17, 0x6C, 0x9E, 0x2D, 0xF2, 0x98],
            [0x32, 0x47, 0xF8, 0x89, 0xA7, 0xB6, 0xDE, 0xCB, 0xCA, 0x3E, 0x28, 0x69, 0x3E, 0x4A, 0xAA, 0xFE],
        ],
        [
            [0x1E, 0x4F, 0x32, 0x23, 0x9D, 0x65, 0xA5, 0x7D, 0x37, 0xD2, 0x26, 0x6D, 0x9A, 0x77, 0x5D, 0x43],
            [0xA3, 0x32, 0xD3, 0xC3, 0xE4, 0x2F, 0x5A, 0x3E, 0x93, 0x1D, 0xD9, 0x91, 0x72, 0x9A, 0x32, 0x1D],
            [0x3F, 0x2A, 0x35, 0x39, 0x9A, 0x37, 0x33, 0x77, 0x67, 0x41, 0x55, 0xEC, 0xF2, 0x8F, 0xD1, 0x9A],
        ],
        [
            [0x12, 0x2D, 0x57, 0xE9, 0x2A, 0x51, 0x8A, 0xFF, 0x5E, 0x3C, 0x78, 0x6B, 0x7C, 0x34, 0xE1, 0x89],
            [0xDD, 0x6D, 0xF2, 0xD9, 0x54, 0x37, 0x85, 0x67, 0x45, 0x22, 0x71, 0x72, 0x19, 0x98, 0x9F, 0xB0],
            [0x12, 0x69, 0x89, 0x65, 0xA1, 0x32, 0xC7, 0x61, 0x36, 0xCC, 0x88, 0xC5, 0xDD, 0x94, 0xEE, 0x91],
        ],
        [
            [0x61, 0x62, 0x33, 0x66, 0x37, 0x36, 0x64, 0x37, 0x39, 0x38, 0x39, 0x32, 0x30, 0x37, 0x66, 0x32],
            [0x32, 0x62, 0x66, 0x35, 0x31, 0x35, 0x62, 0x33, 0x61, 0x39, 0x37, 0x33, 0x37, 0x38, 0x33, 0x35],
        ],
    ];

    public Span<OfpMtkFileInfo> Entries => _fileInfos;
    public OfpMtkHeader Header => _header;
    private ReadOnlySpan<byte> HdrKey => "geyixue"u8;
    public override int FileInfoCount => _fileInfos.Length;
    private byte[]? _key;
    private byte[]? _iv;
    private Aes? _aes;
    private OfpMtkHeader _header;
    private OfpMtkFileInfo[] _fileInfos;
    public override UPFirmwareType FirmwareType => UPFirmwareType.OFP_MTK;


    public OfpMtkUnpacker(Stream source, uint bufferSize, bool leaveOpen = false)
            : base(source, bufferSize,  leaveOpen)
    {
        _fileInfos = [];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _aes?.Dispose();
        }
        base.Dispose(disposing);
    }

    public static void MtkShuffle(
        ReadOnlySpan<byte> key,
        int keyLength,
        Span<byte> input,
        int inputLength
    )
    {
        for (int i = 0; i < inputLength; i++)
        {
            byte k = key[i % keyLength];
            byte h = (byte)(((input[i] & 0xF0) >> 4) | (16 * (input[i] & 0x0F)));
            input[i] = (byte)(k ^ h);
        }
    }

    private void MtkShuffle2(
        ReadOnlySpan<byte> key,
        int keyLength,
        Span<byte> input,
        int inputLength
    )
    {
        for (var i = 0; i < inputLength; i++)
        {
            var tmp = key[i % keyLength] ^ input[i];
            input[i] = (byte)((byte)((tmp & 0xF0) >> 4) | (16 * (tmp & 0xF)));
        }
    }
    private void GetKey(int index, Span<byte> keyDest, Span<byte> ivDest)
    {
        byte[][] kt = KeyTables[index];

        if (kt.Length == 3)
        {
            // 分配栈内存
            Span<byte> obsKey = stackalloc byte[16];
            Span<byte> encAesKey = stackalloc byte[16];
            Span<byte> encAesIv = stackalloc byte[16];
            Span<byte> md5Hash = stackalloc byte[16]; 

            kt[0].CopyTo(obsKey);
            kt[1].CopyTo(encAesKey);
            kt[2].CopyTo(encAesIv);

            MtkShuffle2(obsKey, 16, encAesKey, 16);
            MD5.TryHashData(encAesKey, md5Hash, out _);

            md5Hash.Slice(0, 8).ToHexStringLower().TryUtf8(keyDest);

            MtkShuffle2(obsKey, 16, encAesIv, 16);
            MD5.TryHashData(encAesIv, md5Hash, out _);

            md5Hash.Slice(0, 8).ToHexStringLower().TryUtf8(ivDest);
        }
        else
        {
            kt[0].CopyTo(keyDest);
            kt[1].CopyTo(ivDest);
        }
    }

    private bool BruteKey(ReadOnlySpan<byte> header, Span<byte> keyDest, Span<byte> ivDest)
    {
        Span<byte> plainData = header.Length <= 1024
            ? stackalloc byte[header.Length]
            : new byte[header.Length];

        Span<byte> key = stackalloc byte[16];
        Span<byte> iv = stackalloc byte[16]; 

        _aes ??= Aes.Create();

        for (var keyId = 0; keyId < KeyTables.Length; keyId++)
        {
            GetKey(keyId, key, iv);
            _aes.SetKey(key);

            try
            {
                int written = _aes.DecryptCfb(
                    header, 
                    iv.Slice(0,16),
                    plainData,   
                    System.Security.Cryptography.PaddingMode.None,
                    feedbackSizeInBits: 128
                );

                if (plainData.Slice(0, 3).SequenceEqual("MMM"u8))
                {
                    key.CopyTo(keyDest);
                    iv.CopyTo(ivDest);
                    return true;
                }
            }
            catch
            {
            }
        }
        return false;
    }

    public override bool Parse()
    {
        _source.Seek(0, SeekOrigin.Begin);
        var fileSize = _source.Length;
        var hdrLength = 0x6C;
        Span<byte> tmpHdr = stackalloc byte[16];
        _source.ReadExactly(tmpHdr);

        _key = new byte[16];
        _iv = new byte[16];
        if (!BruteKey(tmpHdr, _key, _iv))
            return false;

        _source.Seek(fileSize - hdrLength, SeekOrigin.Begin);
        Span<byte> hdrData = new byte[hdrLength];
        _source.ReadExactly(hdrData);
        MtkShuffle(HdrKey, HdrKey.Length, hdrData, hdrLength);
        _header = MemoryMarshal.AsRef<OfpMtkHeader>(hdrData);
        var hdr2Length = _header.Hdr2Entries * 0x60;
        _source.Seek(_source.Length - hdr2Length - 0x6C, SeekOrigin.Begin);
        Span<byte> hdr2Data = new byte[hdr2Length];
        _source.ReadExactly(hdr2Data);
        MtkShuffle(HdrKey, HdrKey.Length, hdr2Data, hdr2Data.Length);
        _fileInfos = new OfpMtkFileInfo[_header.Hdr2Entries];
        for (var i = 0; i < _header.Hdr2Entries; i++)
        {
            var fileInfo = MemoryMarshal.AsRef<OfpMtkFileInfo>(hdr2Data.Slice(i * 0x60, 0x60));
            _fileInfos[i] = fileInfo;
        }
        return true;
    }

    public override bool DumpToFile(string file, int id)
    {
        using var fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.Write);
        return DumpToStream(fs, id);
    }

    public override bool DumpToStream(Stream output, int id)
    {
        OfpMtkFileInfo fileInfo = _fileInfos[id];
        var byteToRead = fileInfo.Length;
        var speedTracker = new SpeedTracker(_speed);
        try
        {
            _aes ??= Aes.Create();
            _aes.SetKey(_key);

            _source.Seek((long)fileInfo.Start, SeekOrigin.Begin);
            if (fileInfo.EncLength > 0)
            {
                var encLength = ((fileInfo.EncLength + 15) / 16) * 16; 
                Span<byte> encData = new byte[encLength];
                Span<byte> plainData = new byte[encLength];
                _source.ReadExactly(encData.Slice(0, (int)fileInfo.EncLength));
                _aes.TryDecryptCfb(encData, _iv, plainData, out _, feedbackSizeInBits: 128);
                output.Write(plainData.Slice(0, (int)fileInfo.EncLength));
                byteToRead -= fileInfo.EncLength;
                speedTracker.Update(fileInfo.EncLength);
                _progress?.Invoke(fileInfo.Length - byteToRead, fileInfo.Length);
            }
            while (byteToRead > 0)
            {
                var len = Math.Min(_bufferSize, byteToRead);
                Span<byte> buffer = new byte[len];
                _source.ReadExactly(buffer);
                output.Write(buffer);
                byteToRead -= (uint)len;
                speedTracker.Update(len);
                _progress?.Invoke(fileInfo.Length - byteToRead, fileInfo.Length);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }

        return true;
    }
    public override bool OpenEntryStream(out Stream? output, ushort id)
    {
        output = null;
        if (id < 0 || id >= _fileInfos.Length) return false;

        var fileInfo = _fileInfos[id];

        _aes ??= Aes.Create();
        _aes.SetKey(_key!);

        try
        {
            output = new MtkEntryStream(_source, _aes, _iv!, fileInfo, _leaveOpen);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to open stream: {e.Message}");
            return false;
        }
    }
    public unsafe override int GetEntries(Span<UPFirmwareEntry> entries)
    {
        if (entries.Length < Entries.Length) return -1;

        fixed (UPFirmwareEntry* pDestBase = entries)
        fixed (OfpMtkFileInfo* pSourceBase = Entries)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                UPFirmwareEntry* pDest = pDestBase + i;
                OfpMtkFileInfo* pSrc = pSourceBase + i;

                pDest->Index = (ushort)i;
                pDest->Start = pSrc->Start;
                pDest->Length = pSrc->Length;
                pDest->RealLength = pSrc->Length; 

                Buffer.MemoryCopy(pSrc->Name, pDest->Name, 128, 32);
                Buffer.MemoryCopy(pSrc->FileName, pDest->FileName, 128, 32);
            }
        }

        return Entries.Length;
    }
    public void DumpAllToDir(string dir)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        for (int i = 0; i < _fileInfos.Length; i++)
        {
            var info = _fileInfos[i];
            if (info.Length == 0)
            {
                continue;
            }

            var outputFile = Path.Combine(dir, info.GetFileName());
            DumpToFile(outputFile, i);
        }
    }


}