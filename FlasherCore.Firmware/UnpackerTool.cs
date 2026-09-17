using FlasherCore.Common.Helpers;
using FlasherCore.Common.Types;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware;
using FlasherCore.Firmware.Types;
using FlasherCore.Firmware.Unpackers;
using System;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

public unsafe class UnpackerTool
{
    private FWSession _session;
    private IUnpacker? _unpacker;
    private bool _parseResult;
    private Stream? _source;

    public UnpackerTool(FWSession session)
    {
        _session = session;
    }
    public FlasherCoreResult InitValues()
    {
        if (_source != null)
        {
            _source.Dispose();
        }
        if (_session.InputType == InOutType.File)
        {
            if (string.IsNullOrEmpty(_session.InputFileName))
            {
                return FlasherCoreResult.FileNotFound;
            }
            if (_session.InputFileName.IsFile())
            {
                if (!File.Exists(_session.InputFileName))
                    return FlasherCoreResult.FileNotFound;
                _source = File.OpenRead(_session.InputFileName);
            }
            else if (_session.InputFileName.IsUrl())
            {
                _source = BufferedHttpStream.CreateAsync(_session.InputFileName, (int)_session.BufferSize).GetAwaiter().GetResult();
            }
            else
            {
                return FlasherCoreResult.FileNotFound;
            }
        }
        else
        {
            _source = _session.InputStream.AsStream();
        }

        if (_unpacker == null)
        {
            _unpacker ??= new AutoUnpacker(_source, _session.BufferSize);
        }
        else
        {
            _unpacker.SetStream(_source);
            _unpacker.SetBufferSize(_session.BufferSize);
        }
        _parseResult = _unpacker.Parse();
        if (_parseResult)
        {
            _session.FirmwareType = _unpacker.FirmwareType;
            _unpacker.SetCallback(_session.UserData, _session.Progress, _session.Speed);
        }
        return FlasherCoreResult.Success;
    }
    public FlasherCoreResult OpenEntryStream(ushort id,in InOutOptions options)
    {
        fixed(InOutOptions * ptr = &options)
            return OpenEntryStream(id, ptr);
    }

    public FlasherCoreResult OpenEntryStream(ushort id, InOutOptions* options)
    {
        if (_unpacker == null) return FlasherCoreResult.NotInitialized;
        if (!_parseResult) return FlasherCoreResult.ParseError;
        try
        {
            var option = new InOutOptions();
            option.Type = InOutType.Stream;
            if(!_unpacker.OpenEntryStream(out var stm , id) || stm == null)
            {
                return FlasherCoreResult.DumpError;
            }
            option.Stream = stm.AsInteropStream();
            *options = option;
            return FlasherCoreResult.Success;
        }
        catch
        {
            return FlasherCoreResult.UnknownError;
        }
    }
    public FlasherCoreResult Run(ushort id,in InOutOptions options)
    {
        fixed(InOutOptions* ptr = &options)
        return Run(id, ptr);
    }
    public FlasherCoreResult Run(ushort id ,InOutOptions* options)
    {
        if (_unpacker == null) return FlasherCoreResult.NotInitialized;
        if(!_parseResult) return FlasherCoreResult.ParseError;
        try
        {
            switch (options->Type)
            {
                case InOutType.File:
                    
                    var fileName = options->GetFileName();
                    if (string.IsNullOrEmpty(fileName))
                    {
                        return FlasherCoreResult.InvalidFileName;
                    }
                    return _unpacker.DumpToFile(fileName, id) ? FlasherCoreResult.Success : FlasherCoreResult.DumpError;
                case InOutType.Stream:
                    if(options->Stream.ReadFunc == IntPtr.Zero)
                    {
                        return FlasherCoreResult.InvalidPointer;
                    }
                    var wrapper = options->Stream.AsStream();
                    return _unpacker.DumpToStream(wrapper,id) ? FlasherCoreResult.Success : FlasherCoreResult.DumpError;
                default:
                    return FlasherCoreResult.UnknownError;
            }
        }
        catch
        {
            return FlasherCoreResult.UnknownError;
        }
    }

    public int GetEntryCount()
    {
        return _unpacker?.FileInfoCount ?? 0;
    }
    public FlasherCoreResult GetEntries(Span<UPFirmwareEntry> entry)
    {
        fixed (UPFirmwareEntry* ptr = &entry[0])
            return GetEntries(ptr, entry.Length);
    }
    public FlasherCoreResult GetEntries(UPFirmwareEntry* pointer, int arrayLength)
    {
        if (pointer == null || _unpacker == null) return FlasherCoreResult.InvalidPointer;
        if (!_parseResult) return FlasherCoreResult.ParseError;

        int count = _unpacker.FileInfoCount;
        if (arrayLength < count) return FlasherCoreResult.BufferTooSmall;

        try
        {
            var targetSpan = new Span<UPFirmwareEntry>(pointer, arrayLength);
            int result = _unpacker.GetEntries(targetSpan);
            return result >= 0 ? FlasherCoreResult.Success : FlasherCoreResult.UnknownError;
        }
        catch (Exception e)
        {
            return FlasherCoreResult.UnknownError;
        }
    }
    public void Dispose()
    {
        _unpacker?.Dispose();
    }
}