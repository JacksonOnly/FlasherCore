using FlasherCore.Common;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Models;
using FlasherCore.Firmware.Types;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace FlasherCore.Firmware.Unpackers;

public class KdzUnpacker : BaseUnpacker, IUnpacker<KdzEntry, KdzEntry>, IDisposable
{
    private List<KdzEntry> _entries;
    private KdzEntry _dummyHeader;

    public KdzUnpacker(Stream source, uint bufferSize, bool leaveOpen = false)
        : base(source, bufferSize, leaveOpen)
    {
        _entries = new List<KdzEntry>();
    }

    public KdzEntry Header => _dummyHeader;
    public Span<KdzEntry> Entries => CollectionsMarshal.AsSpan(_entries);
    public override int FileInfoCount => _entries.Count;
    public override UPFirmwareType FirmwareType => UPFirmwareType.KDZ;
    public override unsafe bool Parse()
    {
        _source.Seek(0, SeekOrigin.Begin);
        Span<byte> magicBuf = stackalloc byte[8];
        if (_source.Read(magicBuf) != 8) return false;

        // 1. b"\x28\x05\x00\x00\x34\x31\x25\x80"
        // 2. b"\x18\x05\x00\x00\x32\x79\x44\x50"
        // 3. b"\x28\x05\x00\x00\x24\x38\x22\x25" (最常见)

        ulong magicVal = MemoryMarshal.Read<ulong>(magicBuf);

        bool isValid =
            magicVal == 0x8025313400000528 || // Header 1
            magicVal == 0x5044793200000518 || // Header 2
            magicVal == 0x2522382400000528;   // Header 3

        if (!isValid) return false;


        ulong dataStart = ulong.MaxValue;
        int structSize = sizeof(KdzEntry); 

        byte[] poolBuffer = ArrayPool<byte>.Shared.Rent(structSize);
        try
        {
            Span<byte> bufferSpan = poolBuffer.AsSpan(0, structSize);
            bool last = false;
            bool cont = true;

            while (cont)
            {
                long currentPos = _source.Position;

                if ((ulong)currentPos >= dataStart) break;

                int read = _source.Read(poolBuffer, 0, structSize);
                if (read != structSize) break;

                KdzEntry entry = MemoryMarshal.Read<KdzEntry>(bufferSpan);

                if (entry.Offset < dataStart && entry.Offset > 0)
                {
                    dataStart = entry.Offset;
                }

                _entries.Add(entry);

                int nextByte = _source.ReadByte();

                if (nextByte == 0x03)
                {
                    last = true;
                }
                else if (nextByte == 0x00 || nextByte == -1)
                {
                    cont = false;
                }
                else
                {
                    _source.Seek(-1, SeekOrigin.Current);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(poolBuffer);
        }

        return _entries.Count > 0;
    }
    public override bool DumpToFile(string file, int id)
    {
        using var fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.Write);
        return DumpToStream(fs, id);
    }

    public override bool DumpToStream(Stream output, int id)
    {
        if (id < 0 || id >= _entries.Count) return false;

        var entry = _entries[id];
        var speedTracker = new SpeedTracker(_speed);

        try
        {
            _source.Seek((long)entry.Offset, SeekOrigin.Begin);

            ulong remaining = entry.Length;

            int bufSize = (int)_bufferSize;
            byte[] poolBuffer = ArrayPool<byte>.Shared.Rent(bufSize);

            try
            {
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min((ulong)bufSize, remaining);
                    int read = _source.Read(poolBuffer, 0, toRead);
                    if (read == 0) break;

                    output.Write(poolBuffer, 0, read);

                    remaining -= (ulong)read;
                    speedTracker.Update(read);
                    _progress?.Invoke(entry.Length - remaining, entry.Length);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(poolBuffer);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }

        return true;
    }

    public override unsafe int GetEntries(Span<UPFirmwareEntry> entries)
    {
        if (entries.Length < _entries.Count) return -1;

        var spanSrc = CollectionsMarshal.AsSpan(_entries);

        for (int i = 0; i < spanSrc.Length; i++)
        {
            ref var src = ref spanSrc[i];
            ref var dest = ref entries[i];

            dest.Index = (ushort)i;
            dest.Start = src.Offset;
            dest.Length = src.Length;
            dest.RealLength = src.Length;

            fixed (byte* pSrcName = src.Name)
            fixed (byte* pDestName = dest.Name)
            fixed (byte* pDestFileName = dest.FileName)
            {
                Buffer.MemoryCopy(pSrcName, pDestName, 128, 128);
                Buffer.MemoryCopy(pSrcName, pDestFileName, 128, 128);
            }
        }
        return _entries.Count;
    }
    public override bool OpenEntryStream(out Stream? output, ushort id)
    {
        output = null;
        if (id < 0 || id >= _entries.Count) return false;

        var entry = _entries[id];

        try
        {
            output = new SubStream(_source, (long)entry.Offset, (long)entry.Length);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to open KDZ entry stream: {e.Message}");
            return false;
        }
    }
}