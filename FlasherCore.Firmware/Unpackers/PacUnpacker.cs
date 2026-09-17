using FlasherCore.Common;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Models;
using FlasherCore.Firmware.Types;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Unpackers;

public class PacUnpacker : BaseUnpacker, IUnpacker<PacHeader, PacFileInfo>, IDisposable
{
    public const uint PAC_MAGIC = 0xFFFAFFFA;
    public const int SIZEOF_PAC_HEADER = 2124;
    public const int SIZEOF_FILE_T = 2580;

    public PacHeader Header => _header;
    public Span<PacFileInfo> Entries => _fileInfos.AsSpan();
    public override int FileInfoCount => _fileInfos.Length;
    public override UPFirmwareType FirmwareType => UPFirmwareType.PAC;

    private PacHeader _header;
    private PacFileInfo[] _fileInfos;

    public PacUnpacker(Stream source, uint bufferSize,  bool leaveOpen = false)
        : base(source, bufferSize, leaveOpen)
    {
        _fileInfos = [];
    }

    public override bool Parse()
    {
        try
        {
            if (_source.Length < SIZEOF_PAC_HEADER) return false;
            _source.Seek(0, SeekOrigin.Begin);

            Span<byte> hdrBuffer = stackalloc byte[SIZEOF_PAC_HEADER];
            _source.ReadExactly(hdrBuffer);

            _header = MemoryMarshal.Read<PacHeader>(hdrBuffer);

            if (_header.dwMagic != PAC_MAGIC) return false;
            int count = (int)_header.nFileCount;
            if (count == 0) return true;

            _fileInfos = new PacFileInfo[count];

            Span<byte> entryBuffer = stackalloc byte[SIZEOF_FILE_T];

            for (int i = 0; i < count; i++)
            {
                long pos = SIZEOF_PAC_HEADER + (long)i * SIZEOF_FILE_T;
                _source.Seek(pos, SeekOrigin.Begin);

                _source.ReadExactly(entryBuffer);
                _fileInfos[i] = MemoryMarshal.Read<PacFileInfo>(entryBuffer);
            }

            return true;
        }
        catch
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
        if (id < 0 || id >= _fileInfos.Length) return false;

        PacFileInfo info = _fileInfos[id];

        long offset = info.GetDataOffset();
        long size = info.GetFileSize();

        if (size == 0) return true; 

        _source.Seek(offset, SeekOrigin.Begin);

        long remaining = size;
        long total = size;
        var speedTracker = new SpeedTracker(_speed);

        int bufSize = (int)_bufferSize;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufSize);

        try
        {
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(bufSize, remaining);
                int read = _source.Read(buffer, 0, toRead);
                if (read == 0) break;

                output.Write(buffer, 0, read);
                remaining -= read;

                speedTracker.Update((ulong)read);
                _progress?.Invoke((ulong)(total - remaining), (ulong)total);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return true;
    }


    public unsafe override int GetEntries(Span<UPFirmwareEntry> entries)
    {
        if (entries.Length < _fileInfos.Length) return -1;

        fixed (UPFirmwareEntry* pDestBase = entries)
        fixed (PacFileInfo* pSourceBase = _fileInfos) 
        {
            for (int i = 0; i < _fileInfos.Length; i++)
            {
                UPFirmwareEntry* pDest = pDestBase + i;
                PacFileInfo* pSrc = pSourceBase + i;

                pDest->Index = (ushort)i;
                pDest->Start = (ulong)pSrc->GetDataOffset();
                pDest->RealLength = (ulong)pSrc->GetFileSize();
                pDest->Length = pDest->RealLength; 
                UnsafeUtils.CopyStringBytes(pSrc->szFileID, 512, pDest->Name, 128);
                UnsafeUtils.CopyStringBytes(pSrc->szFileName, 512, pDest->FileName, 128);
            }
        }

        return _fileInfos.Length;
    }

    public override bool OpenEntryStream(out Stream? output, ushort id)
    {
        output = null;
        if (id < 0 || id >= _fileInfos.Length) return false;

        var info = _fileInfos[id];
        long offset = info.GetDataOffset();
        long size = info.GetFileSize();

        try
        {
            // 创建一个包装流，限制读取范围
            output = new PacEntryStream(_source, offset, size, _leaveOpen);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to open PAC entry stream: {e.Message}");
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}