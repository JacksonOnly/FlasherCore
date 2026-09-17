using System;
using System.IO;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware.Types;

namespace FlasherCore.Firmware.Unpackers
{
    public class AutoUnpacker : IUnpacker
    {
        private IUnpacker? _activeUnpacker;
        private Stream _sourceStream;
        private Action<ulong, ulong>? _progressCallback;
        private Action<ulong>? _speedCallback;
        private uint _bufferSize;
        public UPFirmwareType FirmwareType => _activeUnpacker?.FirmwareType ?? UPFirmwareType.None;

        public AutoUnpacker(Stream source, uint bufferSize)
        {
            _sourceStream = source;
            _bufferSize = bufferSize;
        }

        public void Dispose()
        {
            _activeUnpacker?.Dispose();
            if (_sourceStream != null)
            {
                _sourceStream.Dispose();
            }
        }

        public bool Parse()
        {
            if (TryUnpacker(new PayloadUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
            {
                return true;
            }
            else if (_sourceStream.IsZipStream())
            {
                if (TryUnpacker(new OzipUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
                {
                    return true;
                }
                if (TryUnpacker(new ZipUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
                {
                    return true;
                }

            }
            else
            {
                if (TryUnpacker(new OfpMtkUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
                {
                    return true;
                }
                if (TryUnpacker(new OfpQcUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
                {
                    return true;
                }
                if (TryUnpacker(new OpsUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
                {
                    return true;
                }
                if (TryUnpacker(new PacUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
                {
                    return true;
                }
                if (TryUnpacker(new KdzUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
                {
                    return true;
                }
                if (TryUnpacker(new DzUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
                {
                    return true;
                }
                if (TryUnpacker(new UpdateAppUnpacker(_sourceStream, _bufferSize, leaveOpen: true)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryUnpacker(IUnpacker unpacker)
        {
            bool success = false;
            try
            {
                if (_sourceStream.CanSeek)
                    _sourceStream.Seek(0, SeekOrigin.Begin);

                if (unpacker.Parse())
                {
                    _activeUnpacker = unpacker;
                    success = true;
                    return true;
                }
            }
            catch { }
            finally
            {
                if (!success)
                {
                    unpacker.Dispose();
                }
            }
            return false;
        }

        public int FileInfoCount => EnsureParsed() ? _activeUnpacker!.FileInfoCount : 0;

        public int GetEntries(Span<UPFirmwareEntry> entries) =>
            EnsureParsed() ? _activeUnpacker!.GetEntries(entries) : -1;

        public bool DumpToFile(string file, int id) =>
            EnsureParsed() && _activeUnpacker!.DumpToFile(file, id);

        public bool DumpToStream(Stream output, int id) =>
            EnsureParsed() && _activeUnpacker!.DumpToStream(output, id);

        private bool EnsureParsed()
        {
            if (_activeUnpacker == null)
            {
                throw new InvalidOperationException("Unpacker has not parsed successfully.");
            }
            return true;
        }

        public void SetStream(Stream stream)
        {
            _sourceStream?.Dispose();
            _sourceStream = stream;
        }

        public void SetBufferSize(uint bufferSize)
        {
            _bufferSize = bufferSize;
        }

        public void SetCallback(
            nint userData,
            ProgressCallback? progressCallback,
            SpeedCallback? speedCallback
        )
        {
            EnsureParsed();
            _activeUnpacker!.SetCallback(userData, progressCallback, speedCallback);
        }

        public bool OpenEntryStream(out Stream? output, ushort id)
        {
            output = null;
            return EnsureParsed() &&  _activeUnpacker!.OpenEntryStream(out output, id);
        }
    }
}
