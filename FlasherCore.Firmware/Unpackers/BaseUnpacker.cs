using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FlasherCore.Firmware.Unpackers
{
    public abstract class BaseUnpacker : IUnpacker
    {
        protected Action<ulong,ulong>? _progress;
        protected Action<ulong>? _speed;
        protected uint _bufferSize;
        protected Stream _source;
        protected readonly Stopwatch _sw;
        protected readonly bool _leaveOpen;
        public BaseUnpacker(Stream source, uint bufferSize, bool leaveOpen = false)
        {
            _source = source;
            _bufferSize = bufferSize;
            _sw = new Stopwatch();
            _leaveOpen = leaveOpen;
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_leaveOpen && _source != null)
                {
                    _source.Dispose();
                }
            }
        }
        public abstract bool Parse();
        public virtual bool DumpToFile(string file, int id)
        {
            using var fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.Write);
            return DumpToStream(fs, id);
        }
        public abstract bool DumpToStream(Stream output, int id);
        public abstract int GetEntries(Span<UPFirmwareEntry> entries);
        public abstract int FileInfoCount { get; }
        public abstract UPFirmwareType FirmwareType { get; }

        public void SetStream(Stream stream)
        {
            _source?.Dispose();
            _source = stream;
        }

        public void SetBufferSize(uint bufferSize)
        {
            _bufferSize = bufferSize;
        }

        public void SetCallback(IntPtr userData,ProgressCallback? progressCallback, SpeedCallback? speedCallback)
        {
            _progress = (current,total)=>
            {
                progressCallback?.Invoke(userData, current, total);
            };
            _speed = (count) =>
            {
                speedCallback?.Invoke(userData,count);
            };
        }

        public abstract bool OpenEntryStream(out Stream? output, ushort id);
    }
}
