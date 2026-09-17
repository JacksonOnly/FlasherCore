using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Models;
using FlasherCore.Firmware.Types;
using System;
using System.Buffers;
using SharpCompress.Archives.Zip;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FlasherCore.Firmware.Unpackers;

internal class ZipUnpacker : BaseUnpacker, IUnpacker, IDisposable
{
    private ZipArchive? _archive;
    private ZipArchiveEntry[]? _entires;
    public ZipUnpacker(Stream source, uint bufferSize, bool leaveOpen = false) : base(source, bufferSize, leaveOpen)
    {
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing)
        {
            _archive?.Dispose();
        }
        base.Dispose(disposing);
    }
    public override int FileInfoCount => _archive!.Entries.Count;

    public override UPFirmwareType FirmwareType => UPFirmwareType.ZIP;

    public override bool DumpToStream(Stream output, int id)
    {
        var entry= _entires[id];
        using var stm = entry.OpenEntryStream();
        ulong bytesToWrite = (ulong)entry.Size;
        var buffer = ArrayPool<byte>.Shared.Rent((int)_bufferSize);
        try
        {
            var bufferSpan = buffer.AsSpan();
            var speedTracker = new SpeedTracker(_speed);
            while (bytesToWrite > 0)
            {
                int size = (int)Math.Min(_bufferSize, bytesToWrite);
                stm.ReadExactly(bufferSpan.Slice(0, size));
                output.Write(bufferSpan.Slice(0, size));

                speedTracker.Update(size);
                _progress?.Invoke((ulong)entry.Size - bytesToWrite, bytesToWrite);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return true;
    }
    public unsafe override int GetEntries(Span<UPFirmwareEntry> entries)
    {
        if (entries.Length < _archive!.Entries.Count) return -1;

        fixed (UPFirmwareEntry* pDestBase = entries)
        {
            
            for (int i = 0; i < _archive.Entries.Count; i++)
            {
                UPFirmwareEntry* pDest = pDestBase + i;
                var src = _entires![i];
                pDest->Index = (ushort)i;
                pDest->Start = (ulong)i;
                pDest->RealLength = (ulong)src.CompressedSize;
                pDest->Length = (ulong)src.Size;
                Path.GetFileNameWithoutExtension(src.Key)!.CopyToPtr(pDest->Name, 128);
                src.Key!.CopyToPtr(pDest->FileName, 128);
            }
        }

        return _archive!.Entries.Count;

    }
    public override bool Parse()
    {
        var isZip = _source.IsZipStream();
        if(!isZip)
            return false;
        _source.Seek(0,SeekOrigin.Begin);
        _archive = ZipArchive.Open(_source, new SharpCompress.Readers.ReaderOptions()
        {
            BufferSize = (int)_bufferSize,
            LeaveStreamOpen = _leaveOpen,
        });
        _entires = _archive.Entries.ToArray();
        return true;
    }

    public override bool OpenEntryStream(out Stream? output, ushort id)
    {
        output = null;
        if (_entires is null)
            return false;
        var entry = _entires[id];
        output = entry.OpenEntryStream();
        return true;
    }
}
