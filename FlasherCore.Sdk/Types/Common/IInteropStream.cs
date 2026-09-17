using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FlasherCore.Sdk.Types
{
    #region 委托定义 (必须保留以维持函数指针签名)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int StreamReadCallback(IntPtr context, IntPtr buffer, int length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int StreamWriteCallback(IntPtr context, IntPtr buffer, int length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate long StreamSeekCallback(IntPtr context, long offset, int origin);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void StreamSetLengthCallback(IntPtr context, long length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void StreamCloseCallback(IntPtr context);
    #endregion

    [StructLayout(LayoutKind.Sequential)]
    public struct IInteropStream
    {
        public IntPtr Context;
        public IntPtr ReadFunc;
        public IntPtr WriteFunc;
        public IntPtr SeekFunc;
        public IntPtr SetLengthFunc;
        public IntPtr CloseFunc;

    }

    #region 内部支持类：NativeStreamProvider (用于从 Stream 到 Native)
    // 这个类用于保持委托的引用，防止被 GC 回收，同时作为 Context 传递
    internal class NativeStreamProvider : IDisposable
    {
        public readonly Stream BaseStream;
        public readonly bool LeaveOpen;
        public GCHandle Handle;

        // 必须显式持有委托引用，否则函数指针会变成悬空指针
        public readonly StreamReadCallback ReadDel;
        public readonly StreamWriteCallback WriteDel;
        public readonly StreamSeekCallback SeekDel;
        public readonly StreamSetLengthCallback SetLengthDel;
        public readonly StreamCloseCallback CloseDel;

        public NativeStreamProvider(Stream stream, bool leaveOpen)
        {
            BaseStream = stream;
            LeaveOpen = leaveOpen;

            ReadDel = InternalRead;
            WriteDel = InternalWrite;
            SeekDel = InternalSeek;
            SetLengthDel = InternalSetLength;
            CloseDel = InternalClose;

            Handle = GCHandle.Alloc(this);
        }

        private int InternalRead(IntPtr ctx, IntPtr buf, int len)
        {
            try
            {
                // 注意：这里由于不能使用 Span，依然建议使用 InteropStreamWrapper 中提到的预分配 Buffer 优化
                // 简单起见先实现逻辑：
                byte[] temp = new byte[len];
                int read = BaseStream.Read(temp, 0, len);
                if (read > 0) Marshal.Copy(temp, 0, buf, read);
                return read;
            }
            catch { return -1; }
        }

        private int InternalWrite(IntPtr ctx, IntPtr buf, int len)
        {
            try
            {
                byte[] temp = new byte[len];
                Marshal.Copy(buf, temp, 0, len);
                BaseStream.Write(temp, 0, len);
                return len;
            }
            catch { return -1; }
        }

        private long InternalSeek(IntPtr ctx, long off, int orig)
        {
            try { return BaseStream.Seek(off, (SeekOrigin)orig); } catch { return -1; }
        }

        private void InternalSetLength(IntPtr ctx, long len) { BaseStream.SetLength(len); }

        private void InternalClose(IntPtr ctx) { Dispose(); }

        public void Dispose()
        {
            if (Handle.IsAllocated) Handle.Free();
            if (!LeaveOpen) BaseStream.Close();
        }
    }
    #endregion
}