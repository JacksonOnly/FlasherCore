using FlasherCore.Common;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Models;
using FlasherCore.Firmware.Types;
using SharpCompress.Common;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace FlasherCore.Firmware.Unpackers;

public class OfpQcUnpacker : BaseUnpacker, IUnpacker<OfpProFile, OfpQcFileInfo>, IDisposable
{
    private static readonly byte[][][] KeyTables =
    [
        // Group 0: R9s/A57t (V1.4.17/1.4.27)
        [
            [
                0x27,
                0x82,
                0x79,
                0x63,
                0x78,
                0x72,
                0x65,
                0xEF,
                0x89,
                0xD1,
                0x26,
                0xB6,
                0x9A,
                0x49,
                0x5A,
                0x21,
            ],
            [
                0x82,
                0xC5,
                0x02,
                0x03,
                0x28,
                0x5A,
                0x2C,
                0xE7,
                0xD8,
                0xC3,
                0xE1,
                0x98,
                0x38,
                0x3C,
                0xE9,
                0x4C,
            ],
            [
                0x42,
                0x2D,
                0xD5,
                0x39,
                0x91,
                0x81,
                0xE2,
                0x23,
                0x81,
                0x3C,
                0xD8,
                0xEC,
                0xDF,
                0x2E,
                0x4D,
                0x72,
            ],
        ],
        // Group 1: a3s (V1.6.17)
        [
            [
                0xE1,
                0x1A,
                0xA7,
                0xBB,
                0x55,
                0x8A,
                0x43,
                0x6A,
                0x83,
                0x75,
                0xFD,
                0x15,
                0xDD,
                0xD4,
                0x65,
                0x1F,
            ],
            [
                0x77,
                0xDD,
                0xF6,
                0xA0,
                0x69,
                0x68,
                0x41,
                0xF6,
                0xB7,
                0x47,
                0x82,
                0xC0,
                0x97,
                0x83,
                0x51,
                0x69,
            ],
            [
                0xA7,
                0x39,
                0x74,
                0x23,
                0x84,
                0xA4,
                0x4E,
                0x8B,
                0xA4,
                0x52,
                0x07,
                0xAD,
                0x5C,
                0x37,
                0x00,
                0xEA,
            ],
        ],
        // Group 2: V1.5.13
        [
            [
                0x67,
                0x65,
                0x79,
                0x63,
                0x78,
                0x75,
                0x65,
                0xE8,
                0x37,
                0xD2,
                0x26,
                0xB6,
                0x9A,
                0x49,
                0x5D,
                0x21,
            ],
            [
                0xF6,
                0xC5,
                0x02,
                0x03,
                0x51,
                0x5A,
                0x2C,
                0xE7,
                0xD8,
                0xC3,
                0xE1,
                0xF9,
                0x38,
                0xB7,
                0xE9,
                0x4C,
            ],
            [
                0x42,
                0xF2,
                0xD5,
                0x39,
                0x91,
                0x37,
                0xE2,
                0xB2,
                0x81,
                0x3C,
                0xD8,
                0xEC,
                0xDF,
                0x2F,
                0x4D,
                0x72,
            ],
        ],
        // Group 3: R15 Pro / FindX / R17 ...
        [
            [
                0x3C,
                0x2D,
                0x51,
                0x8D,
                0x9B,
                0xF2,
                0xE4,
                0x27,
                0x9D,
                0xC7,
                0x58,
                0xCD,
                0x53,
                0x51,
                0x47,
                0xC3,
            ],
            [
                0x87,
                0xC7,
                0x4A,
                0x29,
                0x70,
                0x9A,
                0xC1,
                0xBF,
                0x23,
                0x82,
                0x27,
                0x6C,
                0x4E,
                0x8D,
                0xF2,
                0x32,
            ],
            [
                0x59,
                0x8D,
                0x92,
                0xE9,
                0x67,
                0x26,
                0x5E,
                0x9B,
                0xCA,
                0xBE,
                0x24,
                0x69,
                0xFE,
                0x4A,
                0x91,
                0x5E,
            ],
        ],
        // Group 4: RM1921EX / Realme X / Realme 5
        [
            [
                0x8F,
                0xB8,
                0xFB,
                0x26,
                0x19,
                0x30,
                0x26,
                0x0B,
                0xE9,
                0x45,
                0xB8,
                0x41,
                0xAE,
                0xFA,
                0x9F,
                0xD4,
            ],
            [
                0xE5,
                0x29,
                0xE8,
                0x2B,
                0x28,
                0xF5,
                0xA2,
                0xF8,
                0x83,
                0x1D,
                0x86,
                0x0A,
                0xE3,
                0x9E,
                0x42,
                0x5D,
            ],
            [
                0x8A,
                0x09,
                0xDA,
                0x60,
                0xED,
                0x36,
                0xF1,
                0x25,
                0xD6,
                0x47,
                0x09,
                0x97,
                0x33,
                0x72,
                0xC1,
                0xCF,
            ],
        ],
        // Group 5: OW19W8AP
        [
            [
                0xE8,
                0xAE,
                0x28,
                0x8C,
                0x01,
                0x92,
                0xC5,
                0x4B,
                0xF1,
                0x0C,
                0x57,
                0x07,
                0xE9,
                0xC4,
                0x70,
                0x5B,
            ],
            [
                0xD6,
                0x4F,
                0xC3,
                0x85,
                0xDC,
                0xD5,
                0x2A,
                0x3C,
                0x9B,
                0x5F,
                0xBA,
                0x86,
                0x50,
                0xF9,
                0x2E,
                0xDA,
            ],
            [
                0x79,
                0x05,
                0x1F,
                0xD8,
                0xD8,
                0xB6,
                0x29,
                0x7E,
                0x2E,
                0x45,
                0x59,
                0xE9,
                0x97,
                0xF6,
                0x3B,
                0x7F,
            ],
        ],
    ];

    public Span<OfpQcFileInfo> Entries => _fileInfos.AsSpan();
    public OfpProFile Header => _header;
    public override int FileInfoCount => _fileInfos.Length;
    public override UPFirmwareType FirmwareType => UPFirmwareType.OFP_QC;

    private byte[]? _key;
    private byte[]? _iv;
    private Aes? _aes;
    private readonly OfpProFile _header;
    private OfpQcFileInfo[] _fileInfos;
    private uint _pageSize;

    public OfpQcUnpacker(
        Stream source,
        uint bufferSize,
        bool leaveOpen = false
    )
        : base(source, bufferSize, leaveOpen)
    {
        _fileInfos = [];
        _header = new OfpProFile();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _aes?.Dispose();
        }
        base.Dispose(disposing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ROL(byte x, byte n, byte bits = 32)
    {
        n = (byte)(bits - n);
        // (1 << n) - 1 等价于 Math.Pow(2, n) - 1
        return (byte)((x >> n) | ((x & ((1 << n) - 1)) << (bits - n)));
    }

    private static void DeObfuscate(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> mask,
        Span<byte> output
    )
    {
        for (var i = 0; i < data.Length; i++)
        {
            output[i] = ROL((byte)(data[i] ^ mask[i]), 4, 8);
        }
    }

    private static void GenerateKeyValues(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        Span<byte> md5Bytes = stackalloc byte[16];
        MD5.TryHashData(source, md5Bytes, out _);

        Span<char> hexChars = stackalloc char[32];
        for (int i = 0; i < 16; i++)
        {
            byte b = md5Bytes[i];
            hexChars[i * 2] = GetHexValue(b >> 4);
            hexChars[i * 2 + 1] = GetHexValue(b & 0x0F);
        }

        Encoding.UTF8.GetBytes(hexChars.Slice(0, 16), dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char GetHexValue(int i) => (char)(i < 10 ? i + 48 : i - 10 + 97);

    private bool TryGenerateKey(int index, Span<byte> keyDest, Span<byte> ivDest)
    {
        if (index >= KeyTables.Length)
            return false;

        var entry = KeyTables[index];
        var mc = entry[0];
        var userKey = entry[1];
        var ivec = entry[2];

        Span<byte> deobfKey = stackalloc byte[userKey.Length];
        Span<byte> deobfIv = stackalloc byte[ivec.Length];

        DeObfuscate(userKey, mc, deobfKey);
        DeObfuscate(ivec, mc, deobfIv);

        GenerateKeyValues(deobfKey, keyDest);
        GenerateKeyValues(deobfIv, ivDest);

        return true;
    }

    public override bool Parse()
    {
        try
        {
            _source.Seek(0, SeekOrigin.Begin);
            long fileSize = _source.Length;
            int pageSize = 0;

            Span<byte> magic = stackalloc byte[4];

            if (fileSize > 0x200)
            {
                _source.Seek(fileSize - 0x200 + 0x10, SeekOrigin.Begin);
                _source.ReadExactly(magic);
                if (BitConverter.ToUInt32(magic) == 0x7CEF)
                    pageSize = 0x200;
            }

            if (fileSize > 0x1000)
            {
                _source.Seek(fileSize - 0x1000 + 0x10, SeekOrigin.Begin);
                _source.ReadExactly(magic);
                if (BitConverter.ToUInt32(magic) == 0x7CEF)
                    pageSize = 0x1000;
            }

            if (pageSize == 0)
                return false;
            _pageSize = (uint)pageSize;

            long xmlMetaOffset = fileSize - pageSize;
            _source.Seek(xmlMetaOffset + 0x14, SeekOrigin.Begin);

            Span<byte> metaBuf = stackalloc byte[8];
            _source.ReadExactly(metaBuf);

            long offset = BitConverter.ToUInt32(metaBuf.Slice(0, 4)) * pageSize;
            uint length = BitConverter.ToUInt32(metaBuf.Slice(4, 4));

            if (length < 200)
            {
                length = (uint)(xmlMetaOffset - offset - 0x57);
            }

            if (length > 20 * 1024 * 1024)
                return false;

            int alignedLength = (int)length;
            if (alignedLength % 16 != 0)
                alignedLength += (16 - (alignedLength % 16));

            var rentArray = ArrayPool<byte>.Shared.Rent(alignedLength);
            var rentDecryptedArray = ArrayPool<byte>.Shared.Rent(alignedLength);
            Span<byte> decryptedData = rentDecryptedArray.AsSpan();
            Span<byte> encXmlData = rentArray.AsSpan();
            string? decryptedXml = null;

            try
            {
                _source.Seek(offset, SeekOrigin.Begin);
                _source.ReadExactly(encXmlData.Slice(0, (int)length));

                Span<byte> tempKey = stackalloc byte[16];
                Span<byte> tempIv = stackalloc byte[16];
                _aes ??= Aes.Create();
                for (int i = 0; i < KeyTables.Length; i++)
                {
                    TryGenerateKey(i, tempKey, tempIv);
                    _aes.SetKey(tempKey);

                    try
                    {
                        if (
                            !_aes.TryDecryptCfb(
                                encXmlData.Slice(0, 64),
                                tempIv,
                                decryptedData,
                                out _,
                                feedbackSizeInBits: 128
                            )
                        )
                        {
                            continue;
                        }
                        if (decryptedData.Slice(0, 5).SequenceEqual("<?xml"u8))
                        {
                            _key = tempKey.ToArray();
                            _iv = tempIv.ToArray();

                            _aes.TryDecryptCfb(
                                encXmlData,
                                tempIv,
                                decryptedData,
                                out _,
                                feedbackSizeInBits: 128
                            );
                            decryptedXml = decryptedData.Slice(0, (int)length - 1).Utf8().TrimEnd();
                            break;
                        }
                    }
                    catch
                    { /* Ignore */
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentArray);
                ArrayPool<byte>.Shared.Return(rentDecryptedArray);
            }

            if (decryptedXml == null)
                return false;

            return ParseXmlStructure(decryptedXml);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Parse Error: {ex.Message}");
            return false;
        }
    }

    private bool ParseXmlStructure(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var root = doc.Element("ProFile");
            if (root == null)
                return false;

            var bi = root.Element("BasicInfo");
            if (bi != null)
            {
                _header.BasicInfo = new Models.Ofp.BasicInfo
                {
                    Brand = bi.Attribute("Brand")?.Value ?? string.Empty,
                    Project = bi.Attribute("Project")?.Value ?? string.Empty,
                    Version = bi.Attribute("Version")?.Value ?? string.Empty,
                };
            }

            var list = new List<OfpQcFileInfo>();
            int index = 0;

            // 修复：参数改为 string?，允许 null 值
            void AddInfo(
                string? name,
                string? fn,
                long rLen,
                long secLen,
                long secOffset,
                string? md5,
                string? sha256,
                OfpQcFileType dType
            )
            {
                ulong start = secOffset >= 0 ? (ulong)secOffset * _pageSize : 0;

                // 处理 Config 等特殊情况
                if (secOffset == -1 && secLen > 0)
                {
                    start = (ulong)secLen * _pageSize;
                }

                ulong length = secLen > 0 ? (ulong)secLen * _pageSize : (ulong)rLen;

                // 修复：处理 null 引用，提供默认值
                string finalName = name ?? fn ?? $"Unknown_{index}";
                string finalFileName = fn ?? name ?? $"file_{index}.bin";

                list.Add(
                    new OfpQcFileInfo
                    {
                        Index = index++,
                        Name = finalName,
                        FileName = finalFileName,
                        RealLength = (ulong)rLen,
                        Length = length,
                        Start = start,
                        Md5 = md5,
                        Sha256 = sha256,
                        DumpType = dType,
                    }
                );
            }

            // 1. Sahara
            foreach (
                var el in root.Element("Sahara")?.Elements("File") ?? Enumerable.Empty<XElement>()
            )
            {
                AddInfo(
                    $"SaharaImage{el.Attribute("Id")?.Value}",
                    el.Attribute("Path")?.Value,
                    (long?)el.Attribute("SizeInByteInSrc") ?? 0,
                    (long?)el.Attribute("SizeInSectorInSrc") ?? 0,
                    (long?)el.Attribute("FileOffsetInSrc") ?? -1,
                    el.Attribute("md5")?.Value,
                    null,
                    OfpQcFileType.DECRYPT_FILE
                );
            }

            // 2. ProgramList
            foreach (
                var el in root.Element("ProgramList")?.Elements("program")
                    ?? Enumerable.Empty<XElement>()
            )
            {
                AddInfo(
                    el.Attribute("label")?.Value, // 传入 null 由 AddInfo 处理
                    el.Attribute("filename")?.Value,
                    (long?)el.Attribute("SizeInByteInSrc") ?? 0,
                    (long?)el.Attribute("SizeInSectorInSrc") ?? 0,
                    (long?)el.Attribute("FileOffsetInSrc") ?? -1,
                    el.Attribute("md5")?.Value,
                    el.Attribute("sha256")?.Value,
                    OfpQcFileType.DECRYPT_FILE
                );
            }

            // 3. Super
            foreach (
                var el in root.Element("Super")?.Elements("program") ?? Enumerable.Empty<XElement>()
            )
            {
                AddInfo(
                    el.Attribute("label")?.Value,
                    el.Attribute("filename")?.Value,
                    (long?)el.Attribute("SizeInByteInSrc") ?? 0,
                    (long?)el.Attribute("SizeInSectorInSrc") ?? 0,
                    (long?)el.Attribute("FileOffsetInSrc") ?? -1,
                    el.Attribute("md5")?.Value,
                    el.Attribute("sha256")?.Value,
                    OfpQcFileType.DECRYPT_FILE
                );
            }

            // 4. Config, Provision, etc.
            void ProcessConfigSection(string sectionName, OfpQcFileType dumpType)
            {
                foreach (
                    var el in root.Element(sectionName)?.Elements("config")
                        ?? Enumerable.Empty<XElement>()
                )
                {
                    var fn = el.Attribute("filename")?.Value;
                    string? name = fn != null ? Path.GetFileNameWithoutExtension(fn) : null;

                    AddInfo(
                        name,
                        fn,
                        (long?)el.Attribute("SizeInByteInSrc") ?? 0,
                        (long?)el.Attribute("SizeInSectorInSrc") ?? 0,
                        -1,
                        el.Attribute("md5")?.Value,
                        el.Attribute("sha256")?.Value,
                        dumpType
                    );
                }
            }

            ProcessConfigSection("Config", OfpQcFileType.DECRYPT_FILE);
            ProcessConfigSection("Provision", OfpQcFileType.DECRYPT_FILE);
            ProcessConfigSection("ChainedTableOfDigests", OfpQcFileType.COPY_FILE);
            ProcessConfigSection("DigestsToSign", OfpQcFileType.COPY_FILE);

            _fileInfos = list.ToArray();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override bool DumpToFile(string file, int id)
    {
        using var fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.Write);
        return DumpToStream(fs, id);
    }

    public override bool DumpToStream(Stream output, int id)
    {
        if (id < 0 || id >= _fileInfos.Length)
            return false;
        var info = _fileInfos[id];

        long byteToRead = (long)info.RealLength;
        long totalBytes = byteToRead;
        var speedTracker = new SpeedTracker(_speed);

        try
        {
            _source.Seek((long)info.Start, SeekOrigin.Begin);

            if (info.DumpType == OfpQcFileType.DECRYPT_FILE)
            {
                _aes ??= Aes.Create();
                _aes.SetKey(_key);

                long decryptBlockSize = 0x40000;
                long actualDecryptSize = Math.Min(decryptBlockSize, byteToRead);
                long readSizeAligned = actualDecryptSize;
                if (readSizeAligned % 16 != 0)
                    readSizeAligned += (16 - (readSizeAligned % 16));

                byte[] rentArray = ArrayPool<byte>.Shared.Rent((int)readSizeAligned);
                byte[] rentPlainArray = ArrayPool<byte>.Shared.Rent((int)readSizeAligned);
                Span<byte> encBuffer = rentArray.AsSpan();
                Span<byte> plainBuffer = rentPlainArray.AsSpan();
                try
                {
                    _source.ReadExactly(encBuffer.Slice(0, (int)actualDecryptSize));
                    _aes.TryDecryptCfb(encBuffer, _iv, plainBuffer, out _, feedbackSizeInBits: 128);
                    output.Write(plainBuffer.Slice(0, (int)actualDecryptSize));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentArray);
                    ArrayPool<byte>.Shared.Return(rentPlainArray);
                }

                byteToRead -= actualDecryptSize;
                speedTracker.Update((ulong)actualDecryptSize);
                _progress?.Invoke((ulong)(totalBytes - byteToRead), (ulong)totalBytes);
            }

            int bufferSize = (int)_bufferSize;
            byte[] rentBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            var copyBuffer = rentBuffer.AsSpan();
            try
            {
                while (byteToRead > 0)
                {
                    int toRead = (int)Math.Min(bufferSize, byteToRead);
                    int read = _source.Read(copyBuffer.Slice(0, toRead));
                    if (read == 0)
                        break;

                    output.Write(copyBuffer.Slice(0, toRead));
                    byteToRead -= read;
                    speedTracker.Update((ulong)read);
                    _progress?.Invoke((ulong)(totalBytes - byteToRead), (ulong)totalBytes);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentBuffer);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Dump Error: {e}");
            return false;
        }

        return true;
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
                continue;

            // 这里 info.Name 可能为 null (虽然 AddInfo 保证了不为空，但类型是可空)，
            // 使用 null合并运算符确保安全
            string safeName = info.FileName ?? info.Name ?? $"file_{i}.bin";
            var outputFile = Path.Combine(dir, safeName);
            DumpToFile(outputFile, i);
        }
    }

    // =================================================================================================
    // Unsafe 接口实现
    // =================================================================================================
    public unsafe override int GetEntries(Span<UPFirmwareEntry> entries)
    {
        if (entries.Length < _fileInfos.Length)
            return -1;

        fixed (UPFirmwareEntry* pDestBase = entries)
        {
            for (int i = 0; i < _fileInfos.Length; i++)
            {
                UPFirmwareEntry* pDest = pDestBase + i;
                var info = _fileInfos[i];

                pDest->Index = (ushort)i;
                pDest->Start = info.Start;
                pDest->Length = info.Length;
                pDest->RealLength = info.RealLength;

                new Span<byte>(pDest->Name, 128).Clear();
                new Span<byte>(pDest->FileName, 128).Clear();

                if (!string.IsNullOrEmpty(info.Name))
                {
                    var destSpan = new Span<byte>(pDest->Name, 128);
                    Encoding.UTF8.GetBytes(info.Name, destSpan);
                }

                if (!string.IsNullOrEmpty(info.FileName))
                {
                    var destSpan = new Span<byte>(pDest->FileName, 128);
                    Encoding.UTF8.GetBytes(info.FileName, destSpan);
                }
            }
        }

        return _fileInfos.Length;
    }
    public override bool OpenEntryStream(out Stream? output, ushort id)
    {
        output = null;
        if (id < 0 || id >= _fileInfos.Length) return false;

        var info = _fileInfos[id];

        if (info.DumpType == OfpQcFileType.DECRYPT_FILE)
        {
            _aes ??= Aes.Create();
            _aes.SetKey(_key!);
        }

        try
        {
            output = new QcEntryStream(_source, _aes, _iv, info, _leaveOpen);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to open QC stream: {e.Message}");
            return false;
        }
    }
}
