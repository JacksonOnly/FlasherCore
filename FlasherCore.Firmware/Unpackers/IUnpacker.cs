using FlasherCore.Firmware.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Firmware.Unpackers
{
    public interface IUnpacker : IDisposable
    {
        bool Parse();
        bool DumpToFile(string file, int id);
        bool DumpToStream(Stream output, int id);
        bool OpenEntryStream(out Stream? output, ushort id);
        int GetEntries(Span<UPFirmwareEntry> entries);
        int FileInfoCount { get; }

        void SetStream(Stream stream);
        void SetBufferSize(uint bufferSize);
        void SetCallback(IntPtr userData, ProgressCallback? progressCallback, SpeedCallback? speedCallback);

        UPFirmwareType FirmwareType { get; }


    }

    public interface IUnpacker<THeader, TFileInfo> : IUnpacker
    {
        THeader Header { get; }
        Span<TFileInfo> Entries { get; }
    }
}
