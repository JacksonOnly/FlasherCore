using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using FlasherCore.Common;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Models;
using FlasherCore.Firmware.Models.Ops;
using FlasherCore.Firmware.Types;
using File = System.IO.File;

namespace FlasherCore.Firmware.Unpackers;

public class OpsUnpacker : BaseUnpacker, IUnpacker<OpsSettings, OpsFileInfo>, IDisposable
{
    private const uint PageSize = 0x200;

    // Initial Key: d1b5e39e5eea049d671dd5abd2afcbaf (Little Endian conversion)
    private static readonly uint[] BaseKey = [0x9ee3b5d1, 0x9d04ea5e, 0xabd51d67, 0xafcbafd2];

    private static readonly byte[] MBox4 =
    [
        0xC4,
        0x5D,
        0x05,
        0x71,
        0x99,
        0xDD,
        0xBB,
        0xEE,
        0x29,
        0xA1,
        0x6D,
        0xC7,
        0xAD,
        0xBF,
        0xA4,
        0x3F,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x0a,
        0x00,
    ];
    private static readonly byte[] MBox5 =
    [
        0x60,
        0x8a,
        0x3f,
        0x2d,
        0x68,
        0x6b,
        0xd4,
        0x23,
        0x51,
        0x0c,
        0xd0,
        0x95,
        0xbb,
        0x40,
        0xe9,
        0x76,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x0a,
        0x00,
    ];
    private static readonly byte[] MBox6 =
    [
        0xAA,
        0x69,
        0x82,
        0x9E,
        0x5D,
        0xDE,
        0xB1,
        0x3D,
        0x30,
        0xBB,
        0x81,
        0xA3,
        0x46,
        0x65,
        0xa3,
        0xe1,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x0a,
        0x00,
    ];

    public static readonly byte[] SBox = Convert.FromBase64String(
        "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"
    );

    public OpsSettings Header => _header;
    public Span<OpsFileInfo> Entries => _fileInfos.AsSpan();
    public override int FileInfoCount => _fileInfos.Length;
    public override UPFirmwareType FirmwareType => UPFirmwareType.OPS;

    private OpsSettings _header;
    private OpsFileInfo[] _fileInfos;
    private byte[] _currentMBox;

    public OpsUnpacker(
        Stream source,
        uint bufferSize,
        bool leaveOpen = false
    )
        : base(source, bufferSize, leaveOpen)
    {
        _fileInfos = [];
        _header = new OpsSettings();
        _currentMBox = MBox5;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GSbox(uint offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(SBox.AsSpan((int)offset));
    }

    public static void KeyUpdate(Span<uint> iv1, ReadOnlySpan<byte> asbox)
    {
        uint d = iv1[0] ^ asbox[0];
        uint a = iv1[1] ^ asbox[1];
        uint b = iv1[2] ^ asbox[2];
        uint c = iv1[3] ^ asbox[3];

        var e =
            GSbox(((b >> 0x10) & 0xff) * 8 + 2)
            ^ GSbox(((a >> 8) & 0xff) * 8 + 3)
            ^ GSbox((c >> 0x18) * 8 + 1)
            ^ GSbox((d & 0xff) * 8)
            ^ asbox[4];

        var h =
            GSbox(((c >> 0x10) & 0xff) * 8 + 2)
            ^ GSbox(((b >> 8) & 0xff) * 8 + 3)
            ^ GSbox((d >> 0x18) * 8 + 1)
            ^ GSbox((a & 0xff) * 8)
            ^ asbox[5];

        var i =
            GSbox(((d >> 0x10) & 0xff) * 8 + 2)
            ^ GSbox(((c >> 8) & 0xff) * 8 + 3)
            ^ GSbox((a >> 0x18) * 8 + 1)
            ^ GSbox((b & 0xff) * 8)
            ^ asbox[6];

        a =
            GSbox(((d >> 8) & 0xff) * 8 + 3)
            ^ GSbox(((a >> 0x10) & 0xff) * 8 + 2)
            ^ GSbox((b >> 0x18) * 8 + 1)
            ^ GSbox((c & 0xff) * 8)
            ^ asbox[7];

        var g = 8;
        var loopCount = asbox[0x3c] - 2;

        for (int f = 0; f < loopCount; f++)
        {
            d = e >> 0x18;
            uint m = h >> 0x10;
            uint s = h >> 0x18;
            uint z = e >> 0x10;
            uint l = i >> 0x18;
            uint t = e >> 8;

            e =
                GSbox(((i >> 0x10) & 0xff) * 8 + 2)
                ^ GSbox(((h >> 8) & 0xff) * 8 + 3)
                ^ GSbox((a >> 0x18) * 8 + 1)
                ^ GSbox((e & 0xff) * 8)
                ^ asbox[g];
            h =
                GSbox(((a >> 0x10) & 0xff) * 8 + 2)
                ^ GSbox(((i >> 8) & 0xff) * 8 + 3)
                ^ GSbox(d * 8 + 1)
                ^ GSbox((h & 0xff) * 8)
                ^ asbox[g + 1];
            i =
                GSbox((z & 0xff) * 8 + 2)
                ^ GSbox(((a >> 8) & 0xff) * 8 + 3)
                ^ GSbox(s * 8 + 1)
                ^ GSbox((i & 0xff) * 8)
                ^ asbox[g + 2];
            a =
                GSbox((t & 0xff) * 8 + 3)
                ^ GSbox((m & 0xff) * 8 + 2)
                ^ GSbox(l * 8 + 1)
                ^ GSbox((a & 0xff) * 8)
                ^ asbox[g + 3];
            g += 4;
        }

        // Write back to iv1
        iv1[0] =
            (GSbox(((i >> 0x10) & 0xff) * 8) & 0xff0000)
            ^ (GSbox(((h >> 8) & 0xff) * 8 + 1) & 0xff00)
            ^ (GSbox((a >> 0x18) * 8 + 3) & 0xff000000)
            ^ (GSbox((e & 0xff) * 8 + 2) & 0xFF)
            ^ asbox[g];
        iv1[1] =
            (GSbox(((a >> 0x10) & 0xff) * 8) & 0xff0000)
            ^ (GSbox(((i >> 8) & 0xff) * 8 + 1) & 0xff00)
            ^ (GSbox((e >> 0x18) * 8 + 3) & 0xff000000)
            ^ (GSbox((h & 0xff) * 8 + 2) & 0xFF)
            ^ asbox[g + 3];
        iv1[2] =
            (GSbox(((e >> 0x10) & 0xff) * 8) & 0xff0000)
            ^ (GSbox(((a >> 8) & 0xff) * 8 + 1) & 0xff00)
            ^ (GSbox((h >> 0x18) * 8 + 3) & 0xff000000)
            ^ (GSbox((i & 0xff) * 8 + 2) & 0xFF)
            ^ asbox[g + 2];
        iv1[3] =
            (GSbox(((h >> 0x10) & 0xff) * 8) & 0xff0000)
            ^ (GSbox(((e >> 8) & 0xff) * 8 + 1) & 0xff00)
            ^ (GSbox((i >> 0x18) * 8 + 3) & 0xff000000)
            ^ (GSbox((a & 0xff) * 8 + 2) & 0xFF)
            ^ asbox[g + 1];
    }

    private void ProcessBlockAndWrite(ReadOnlySpan<byte> input, Span<uint> rkey, Stream output)
    {
        int inputPtr = 0;
        int length = input.Length;
        int pos = 0; // Fixed as 0 based on original usage logic for files

        // Temp buffer for writing 4-byte uints
        Span<byte> decBytes = stackalloc byte[4];

        // --- Phase 1: 16-byte blocks ---
        if (length > 0xF)
        {
            int blockProcessLen = length;
            // 循环直到剩余不足 16 字节
            while (inputPtr + 0x10 <= blockProcessLen)
            {
                // Update key state based on current MBox
                KeyUpdate(rkey, _currentMBox);

                // Process 16 bytes (4 x uint32)
                int slen = 4; // ((0xF - 0) >> 2) + 1 = 4
                for (int i = 0; i < slen; i++)
                {
                    int readOffset = inputPtr + (i * 4);
                    uint cipherVal = BinaryPrimitives.ReadUInt32LittleEndian(
                        input.Slice(readOffset)
                    );

                    // XOR Decrypt
                    uint plainVal = rkey[i] ^ cipherVal;

                    BinaryPrimitives.WriteUInt32LittleEndian(decBytes, plainVal);
                    output.Write(decBytes);

                    // Update key with Ciphertext (Feedback mode)
                    rkey[i] = cipherVal;
                }

                length -= 0x10;
                inputPtr += 0x10;
            }
        }

        // --- Phase 2: Remaining bytes (4-byte chunks) ---
        if (length != 0)
        {
            // Update key state based on SBox
            KeyUpdate(rkey, SBox);

            int j = 0;
            int m = 0;
            Span<byte> padBuf = stackalloc byte[4];

            while (length > 0)
            {
                uint cipherVal = 0;
                int readLen = Math.Min(4, length);

                if (readLen == 4)
                {
                    cipherVal = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(inputPtr + j));
                }
                else
                {
                    padBuf.Clear();
                    input.Slice(inputPtr + j, readLen).CopyTo(padBuf);
                    cipherVal = BinaryPrimitives.ReadUInt32LittleEndian(padBuf);
                }

                uint plainVal = cipherVal ^ rkey[m];

                // Write aligned 4 bytes (original behavior usually aligns output)
                BinaryPrimitives.WriteUInt32LittleEndian(decBytes, plainVal);
                output.Write(decBytes);

                rkey[m] = cipherVal;

                length -= 4;
                j += 4;
                m++;
            }
        }
    }

    public override bool Parse()
    {
        string xml = string.Empty;
        byte[][] mboxes = [MBox5, MBox6, MBox4];
        string[] mboxNames = ["MBox5", "MBox6", "MBox4"];
        if (_source.Length < 0x200)
            return false;
        _source.Seek(_source.Length - 0x200, SeekOrigin.Begin);
        Span<byte> hdr = stackalloc byte[0x200];
        _source.ReadExactly(hdr);
        uint xmlLength = BinaryPrimitives.ReadUInt32LittleEndian(hdr.Slice(0x18));
        if (xmlLength == 0 || xmlLength > 1.5 * 1024 * 1024)
            return false;
        for (int i = 0; i < mboxes.Length; i++)
        {
            _currentMBox = mboxes[i];
            if (TryExtractXml(xmlLength, out xml))
            {
                return ParseSettingsAndBuildInfo(xml);
            }
        }

        return false;
    }

    private bool TryExtractXml(long xmlLength, out string xmlContent)
    {
        xmlContent = string.Empty;

        long xmlPad = 0x200 - (xmlLength % 0x200);
        long totalReadLen = xmlLength + xmlPad;
        long startPos = _source.Length - 0x200 - totalReadLen;

        if (startPos < 0)
            return false;

        _source.Seek(startPos, SeekOrigin.Begin);
        byte[] encBuffer = ArrayPool<byte>.Shared.Rent((int)totalReadLen);

        try
        {
            _source.ReadExactly(encBuffer, 0, (int)totalReadLen);

            using var ms = new MemoryStream((int)xmlLength);

            Span<uint> rkey = stackalloc uint[4];
            BaseKey.CopyTo(rkey);

            ProcessBlockAndWrite(encBuffer.AsSpan(0, (int)totalReadLen), rkey, ms);

            ms.Position = 0;
            Span<byte> checkBuf = stackalloc byte[64];
            int read = ms.Read(checkBuf);
            string preview = Encoding.UTF8.GetString(checkBuf.Slice(0, read));

            if (
                preview.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || preview.Contains("<Setting", StringComparison.OrdinalIgnoreCase)
            )
            {
                ms.Position = 0;
                using var reader = new StreamReader(ms, Encoding.UTF8, leaveOpen: true);
                char[] chars = new char[xmlLength];
                int count = reader.Read(chars, 0, (int)xmlLength);
                xmlContent = new string(chars, 0, count - 3).TrimEnd('\0');
                return true;
            }
        }
        catch { }
        finally
        {
            ArrayPool<byte>.Shared.Return(encBuffer);
        }

        return false;
    }

    private bool ParseSettingsAndBuildInfo(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Element("Setting");
            if (root == null)
                return false;

            // BasicInfo
            var bi = root.Element("BasicInfo");
            _header.BasicInfo = new BasicInfo
            {
                Project = bi?.Attribute("Project")?.Value ?? "",
                Version = bi?.Attribute("Version")?.Value ?? "",
                MemoryName = bi?.Attribute("MemoryName")?.Value ?? "",
                TargetName = bi?.Attribute("TargetName")?.Value ?? "",
                BackupPartId = bi?.Attribute("BackupPartId")?.Value ?? "",
                MinToolVersion = bi?.Attribute("MinToolVersion")?.Value ?? "",
                ModelVerifyRandom = bi?.Attribute("ModelVerifyRandom")?.Value ?? "",
                ModelVerifyHashToken = bi?.Attribute("ModelVerifyHashToken")?.Value ?? "",
                GrowLastPartToFillDisk = Convert.ToBoolean(
                    bi?.Attribute("GrowLastPartToFillDisk")?.Value ?? "false"
                ),
                LogEnable = Convert.ToBoolean(bi?.Attribute("LogEnable")?.Value ?? "false"),
                LogPositionIndex = Convert.ToInt32(bi?.Attribute("LogPositionIndex")?.Value ?? "0"),
                DelayStartTime = Convert.ToInt32(bi?.Attribute("DelayStartTime")?.Value ?? "0"),
                UseGPT = Convert.ToBoolean(bi?.Attribute("UseGPT")?.Value ?? "false"),
                CheckImage = Convert.ToBoolean(bi?.Attribute("CheckImage")?.Value ?? "false"),
                CheckHwVersion = Convert.ToBoolean(
                    bi?.Attribute("CheckHwVersion")?.Value ?? "false"
                ),
                NeedUsbDownload = Convert.ToBoolean(
                    bi?.Attribute("NeedUsbDownload")?.Value ?? "false"
                ),
                BackupPart = Convert.ToBoolean(bi?.Attribute("BackupPart")?.Value ?? "false"),
                ChipType = Convert.ToInt32(bi?.Attribute("ChipType")?.Value ?? "0"),
                CheckRfVersion = Convert.ToBoolean(
                    bi?.Attribute("CheckRfVersion")?.Value ?? "false"
                ),
                SkipCheckHWVerByCustFlag = Convert.ToBoolean(
                    bi?.Attribute("SkipCheckHWVerByCustFlag")?.Value ?? "false"
                ),
                ParamVersion = Convert.ToInt32(bi?.Attribute("ParamVersion")?.Value ?? "0"),
                ModelVerifyVersion = Convert.ToInt32(
                    bi?.Attribute("ModelVerifyVersion")?.Value ?? "0"
                ),
                SkipImgSHA256Check = Convert.ToBoolean(
                    bi?.Attribute("SkipImgSHA256Check")?.Value ?? "false"
                ),
            };

            // Program Groups
            var programGroups = root.Elements()
                .Where(it => it.Name.LocalName.StartsWith("Program"))
                .ToList();
            _header.Programs = new List<Models.Ops.Program>[programGroups.Count];
            foreach (var group in programGroups)
            {
                if (
                    !int.TryParse(group.Name.LocalName.Replace("Program", ""), out int idx)
                    || idx >= _header.Programs.Length
                )
                    continue;
                _header.Programs[idx] = new List<Models.Ops.Program>();
                foreach (var el in group.Elements())
                {
                    _header
                        .Programs[idx]
                        .Add(
                            new Models.Ops.Program
                            {
                                Filename = el.Attribute("filename")?.Value ?? "",
                                Label = el.Attribute("label")?.Value ?? "",
                                Sha256 = el.Attribute("Sha256")?.Value ?? "",
                                StartByteHex = el.Attribute("start_byte_hex")?.Value ?? "",
                                StartSector = el.Attribute("start_sector")?.Value ?? "",
                                FileOffsetInSrc = Convert.ToInt32(
                                    el.Attribute("FileOffsetInSrc")?.Value ?? "-1"
                                ),
                                SizeInByteInSrc = Convert.ToInt64(
                                    el.Attribute("SizeInByteInSrc")?.Value ?? "0"
                                ),
                                SizeInSectorInSrc = Convert.ToInt32(
                                    el.Attribute("SizeInSectorInSrc")?.Value ?? "0"
                                ),
                                SECTOR_SIZE_IN_BYTES = Convert.ToInt32(
                                    el.Attribute("SECTOR_SIZE_IN_BYTES")?.Value ?? "512"
                                ),
                                FileSectorOffset = Convert.ToInt32(
                                    el.Attribute("file_sector_offset")?.Value ?? "0"
                                ),
                                Sparse = Convert.ToBoolean(
                                    el.Attribute("sparse")?.Value ?? "false"
                                ),
                                PartOfSingleImage = Convert.ToBoolean(
                                    el.Attribute("partofsingleimage")?.Value ?? "false"
                                ),
                                ReadBackVerify = Convert.ToBoolean(
                                    el.Attribute("readbackverify")?.Value ?? "false"
                                ),
                                SizeInKB = Convert.ToDouble(
                                    el.Attribute("size_in_KB")?.Value ?? "0"
                                ),
                                NumPartitionSectors = Convert.ToInt32(
                                    el.Attribute("num_partition_sectors")?.Value ?? "0"
                                ),
                                PhysicalPartitionNumber = Convert.ToInt32(
                                    el.Attribute("physical_partition_number")?.Value ?? "0"
                                ),
                            }
                        );
                }
            }

            // Patches
            var patchGroups = root.Elements()
                .Where(it => it.Name.LocalName.StartsWith("Patch"))
                .ToList();
            _header.Patches = new List<Models.Ops.Patch>[patchGroups.Count];
            foreach (var group in patchGroups)
            {
                if (
                    !int.TryParse(group.Name.LocalName.Replace("Patch", ""), out int idx)
                    || idx >= _header.Patches.Length
                )
                    continue;
                _header.Patches[idx] = new List<Models.Ops.Patch>();
                foreach (var el in group.Elements())
                {
                    _header
                        .Patches[idx]
                        .Add(
                            new Models.Ops.Patch
                            {
                                Filename = el.Attribute("filename")?.Value ?? "",
                                Value = el.Attribute("value")?.Value ?? "",
                                What = el.Attribute("what")?.Value ?? "",
                                StartSector = el.Attribute("start_sector")?.Value ?? "",
                                ByteOffset = Convert.ToInt32(
                                    el.Attribute("byte_offset")?.Value ?? "0"
                                ),
                                SECTOR_SIZE_IN_BYTES = Convert.ToInt32(
                                    el.Attribute("SECTOR_SIZE_IN_BYTES")?.Value ?? "512"
                                ),
                                PhysicalPartitionNumber = Convert.ToInt32(
                                    el.Attribute("physical_partition_number")?.Value ?? "0"
                                ),
                                SizeInBytes = Convert.ToInt32(
                                    el.Attribute("size_in_bytes")?.Value ?? "0"
                                ),
                            }
                        );
                }
            }

            // Sahara & UFS (Simplified for brevity, similar structure)
            var saharaList = root.Element("SAHARA")?.Elements("File");
            if (saharaList != null)
            {
                _header.Sahara = new Sahara { Files = new List<Models.Ops.File>() };
                foreach (var f in saharaList)
                {
                    _header.Sahara.Files.Add(
                        new Models.Ops.File
                        {
                            Id = Convert.ToInt32(f.Attribute("Id")?.Value ?? "0"),
                            Path = f.Attribute("Path")?.Value ?? "",
                            FileOffsetInSrc = Convert.ToInt32(
                                f.Attribute("FileOffsetInSrc")?.Value ?? "-1"
                            ),
                            SizeInSectorInSrc = Convert.ToInt32(
                                f.Attribute("SizeInSectorInSrc")?.Value ?? "0"
                            ),
                            SizeInByteInSrc = Convert.ToInt64(
                                f.Attribute("SizeInByteInSrc")?.Value ?? "0"
                            ),
                        }
                    );
                }
            }

            var ufsList = root.Element("UFS_PROVISION")?.Elements("File");
            if (ufsList != null)
            {
                _header.UfsProvision = new UfsProvision { Files = new List<Models.Ops.File>() };
                foreach (var f in ufsList)
                {
                    _header.UfsProvision.Files.Add(
                        new Models.Ops.File
                        {
                            Id = Convert.ToInt32(f.Attribute("Id")?.Value ?? "0"),
                            Name = f.Attribute("Name")?.Value ?? "",
                            Path = f.Attribute("Path")?.Value ?? "",
                            FileOffsetInSrc = Convert.ToInt32(
                                f.Attribute("FileOffsetInSrc")?.Value ?? "-1"
                            ),
                            SizeInSectorInSrc = Convert.ToInt32(
                                f.Attribute("SizeInSectorInSrc")?.Value ?? "0"
                            ),
                            SizeInByteInSrc = Convert.ToInt64(
                                f.Attribute("SizeInByteInSrc")?.Value ?? "0"
                            ),
                        }
                    );
                }
            }

            BuildFileInfos();
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private void BuildFileInfos()
    {
        var list = new List<OpsFileInfo>();
        int index = 0;

        void AddInfo(
            string name,
            string filename,
            ulong realLen,
            int sectorOffset,
            int sectorSize,
            OpsFileType type,
            string? sha256 = null
        )
        {
            ulong start =
                sectorOffset > -1
                    ? (ulong)(sectorOffset * PageSize)
                    : (ulong)(sectorSize * PageSize);
            ulong len = sectorSize > -1 ? (ulong)(sectorSize * PageSize) : realLen;
            list.Add(
                new OpsFileInfo
                {
                    Index = index++,
                    Name = name,
                    FileName = filename,
                    RealLength = realLen,
                    Start = start,
                    Length = len,
                    DumpType = type,
                    Sha256 = sha256,
                }
            );
        }

        if (_header.Sahara?.Files != null)
            foreach (var f in _header.Sahara.Files)
                AddInfo(
                    $"SaharaImage{f.Id}",
                    f.Path,
                    (ulong)f.SizeInByteInSrc,
                    f.FileOffsetInSrc,
                    f.SizeInSectorInSrc,
                    OpsFileType.DECRYPT_FILE
                );

        if (_header.Programs != null)
            foreach (var group in _header.Programs)
            foreach (var p in group)
                AddInfo(
                    p.Label,
                    p.Filename,
                    (ulong)p.SizeInByteInSrc,
                    p.FileOffsetInSrc,
                    p.SizeInSectorInSrc,
                    OpsFileType.COPY_FILE,
                    p.Sha256
                );

        if (_header.UfsProvision?.Files != null)
            foreach (var f in _header.UfsProvision.Files)
                AddInfo(
                    f.Name,
                    f.Path,
                    (ulong)f.SizeInByteInSrc,
                    f.FileOffsetInSrc,
                    f.SizeInSectorInSrc,
                    OpsFileType.COPY_FILE
                );

        // Virtual Files
        if (_header.Programs != null)
            for (int i = 0; i < _header.Programs.Length; i++)
                list.Add(
                    new OpsFileInfo
                    {
                        Index = index++,
                        Name = $"RawProgram{i}",
                        FileName = $"rawprogram{i}.xml",
                        Length = (ulong)i,
                        RealLength = (ulong)i,
                        DumpType = OpsFileType.PROGRAM,
                    }
                );

        if (_header.Patches != null)
            for (int i = 0; i < _header.Patches.Length; i++)
                list.Add(
                    new OpsFileInfo
                    {
                        Index = index++,
                        Name = $"Patch{i}",
                        FileName = $"patch{i}.xml",
                        Length = (ulong)i,
                        RealLength = (ulong)i,
                        DumpType = OpsFileType.PATCH,
                    }
                );

        _fileInfos = list.ToArray();
    }

    public override bool DumpToStream(Stream output, int id)
    {
        if (id < 0 || id >= _fileInfos.Length)
            return false;
        var info = _fileInfos[id];

        try
        {
            switch (info.DumpType)
            {
                case OpsFileType.DECRYPT_FILE:
                    return DumpDecryptedFile(output, info);
                case OpsFileType.COPY_FILE:
                    return DumpCopyFile(output, info);
                case OpsFileType.PROGRAM:
                    return GenerateProgramXml(output, (int)info.RealLength);
                case OpsFileType.PATCH:
                    return GeneratePatchXml(output, (int)info.RealLength);
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Dump Error: {ex}");
            return false;
        }
    }

    private bool DumpDecryptedFile(Stream output, OpsFileInfo info)
    {
        long start = (long)info.Start;
        long length = (long)info.RealLength;
        var speedTracker = new SpeedTracker(_speed);

        long readLen = length;
        if (readLen % 4 != 0)
            readLen += (4 - (readLen % 4));

        _source.Seek(start, SeekOrigin.Begin);

        int bufferSize = (int)_bufferSize;
        if (bufferSize % 16 != 0)
            bufferSize = (bufferSize / 16 + 1) * 16;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        long remaining = readLen;

        // 关键：在循环外初始化密钥，保持状态延续
        Span<uint> rkey = stackalloc uint[4];
        BaseKey.CopyTo(rkey);

        try
        {
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(bufferSize, remaining);
                _source.ReadExactly(buffer, 0, toRead);

                // 解密当前块 (rkey 状态会在内部更新并传递给下一次)
                ProcessBlockAndWrite(buffer.AsSpan(0, toRead), rkey, output);

                remaining -= toRead;
                speedTracker.Update((ulong)toRead);
                _progress?.Invoke((ulong)(readLen - remaining), (ulong)readLen);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return true;
    }

    private bool DumpCopyFile(Stream output, OpsFileInfo info)
    {
        _source.Seek((long)info.Start, SeekOrigin.Begin);
        long remaining = (long)info.RealLength;
        long total = remaining;
        var speedTracker = new SpeedTracker(_speed);

        int bufSize = (int)_bufferSize;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufSize);

        try
        {
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(bufSize, remaining);
                int read = _source.Read(buffer, 0, toRead);
                if (read == 0)
                    break;

                output.Write(buffer, 0, read);
                remaining -= read;
                speedTracker.Update((ulong)read);
                _progress?.Invoke((ulong)(total - remaining), (ulong)total);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return true;
    }

    // 生成 XML 虚拟文件 (Program)
    private bool GenerateProgramXml(Stream output, int index)
    {
        if (index >= _header.Programs.Length)
            return false;
        var programs = _header.Programs[index];
        var doc = new XDocument();
        var root = new XElement("data");

        foreach (var t in programs)
        {
            var el = new XElement("program");
            el.SetAttributeValue("SECTOR_SIZE_IN_BYTES", t.SECTOR_SIZE_IN_BYTES);
            el.SetAttributeValue("file_sector_offset", t.FileSectorOffset);
            el.SetAttributeValue("filename", t.Filename);
            el.SetAttributeValue("label", t.Label);
            el.SetAttributeValue("num_partition_sectors", t.NumPartitionSectors);
            el.SetAttributeValue("partofsingleimage", t.PartOfSingleImage.ToString().ToLower());
            el.SetAttributeValue("physical_partition_number", t.PhysicalPartitionNumber);
            el.SetAttributeValue("readbackverify", t.ReadBackVerify.ToString().ToLower());
            el.SetAttributeValue("size_in_KB", t.SizeInKB.ToString("0.0"));
            el.SetAttributeValue("sparse", t.Sparse.ToString().ToLower());
            el.SetAttributeValue("start_byte_hex", t.StartByteHex);
            el.SetAttributeValue("start_sector", t.StartSector);
            root.Add(el);
        }
        doc.Add(root);
        doc.Save(output);
        return true;
    }

    // 生成 XML 虚拟文件 (Patch)
    private bool GeneratePatchXml(Stream output, int index)
    {
        if (index >= _header.Patches.Length)
            return false;
        var patches = _header.Patches[index];
        var doc = new XDocument();
        var root = new XElement("patches");

        foreach (var t in patches)
        {
            var el = new XElement("patch");
            el.SetAttributeValue("SECTOR_SIZE_IN_BYTES", t.SECTOR_SIZE_IN_BYTES);
            el.SetAttributeValue("byte_offset", t.ByteOffset);
            el.SetAttributeValue("filename", t.Filename);
            el.SetAttributeValue("physical_partition_number", t.PhysicalPartitionNumber);
            el.SetAttributeValue("size_in_bytes", t.SizeInBytes);
            el.SetAttributeValue("start_sector", t.StartSector);
            el.SetAttributeValue("value", t.Value);
            el.SetAttributeValue("what", t.What);
            root.Add(el);
        }
        doc.Add(root);
        doc.Save(output);
        return true;
    }

    public override bool DumpToFile(string file, int id)
    {
        using var fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.Write);
        return DumpToStream(fs, id);
    }

    public override unsafe int GetEntries(Span<UPFirmwareEntry> entries)
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
                    Encoding.UTF8.TryGetBytes(info.Name, new Span<byte>(pDest->Name, 128), out _);
                }
                if (!string.IsNullOrEmpty(info.FileName))
                {
                    Encoding.UTF8.TryGetBytes(
                        info.FileName,
                        new Span<byte>(pDest->FileName, 128),
                        out _
                    );
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

        try
        {
            if (info.DumpType == OpsFileType.PROGRAM || info.DumpType == OpsFileType.PATCH)
            {
                var ms = new MemoryStream();
                if (info.DumpType == OpsFileType.PROGRAM)
                    GenerateProgramXml(ms, (int)info.RealLength);
                else
                    GeneratePatchXml(ms, (int)info.RealLength);

                ms.Position = 0;
                output = ms;
                return true;
            }

            output = new OpsEntryStream(_source, info, _currentMBox, BaseKey, _leaveOpen);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to open OPS stream: {e.Message}");
            return false;
        }
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
