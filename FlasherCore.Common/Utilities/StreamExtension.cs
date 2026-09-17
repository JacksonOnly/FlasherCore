using FlasherCore.Common.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Common.Utilities;

public static class StreamExtension
{
    public static IInteropStream AsInteropStream(this Stream stream,bool leaveOpen = false)
    {
        if (stream == null) throw new ArgumentNullException("stream");
        NativeStreamProvider provider = new NativeStreamProvider(stream, leaveOpen);
        IInteropStream native;
        native.Context = GCHandle.ToIntPtr(provider.Handle);
        native.ReadFunc = stream.CanRead ? Marshal.GetFunctionPointerForDelegate(provider.ReadDel) : IntPtr.Zero;
        native.WriteFunc = stream.CanWrite ? Marshal.GetFunctionPointerForDelegate(provider.WriteDel) : IntPtr.Zero;
        native.SeekFunc = stream.CanSeek ? Marshal.GetFunctionPointerForDelegate(provider.SeekDel) : IntPtr.Zero;
        native.SetLengthFunc = stream.CanWrite ? Marshal.GetFunctionPointerForDelegate(provider.SetLengthDel) : IntPtr.Zero;
        native.CloseFunc = Marshal.GetFunctionPointerForDelegate(provider.CloseDel);
        return native;
    }
    public static Stream AsStream(this IInteropStream stream, bool leaveOpen = false)
    {
        return new InteropStreamWrapper(stream, leaveOpen);
    }
}
