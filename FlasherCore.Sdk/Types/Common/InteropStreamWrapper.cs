using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FlasherCore.Sdk.Types
{
    public class InteropStreamWrapper : Stream
    {
        private IInteropStream _native;
        private readonly bool _leaveOpen;

        private readonly StreamReadCallback _readDel;
        private readonly StreamWriteCallback _writeDel;
        private readonly StreamSeekCallback _seekDel;
        private readonly StreamSetLengthCallback _setLengthDel;
        private readonly StreamCloseCallback _closeDel;

        public InteropStreamWrapper(IInteropStream native, bool leaveOpen)
        {
            _native = native;
            _leaveOpen = leaveOpen;

            if (_native.ReadFunc != IntPtr.Zero) _readDel = (StreamReadCallback)Marshal.GetDelegateForFunctionPointer(_native.ReadFunc, typeof(StreamReadCallback));
            if (_native.WriteFunc != IntPtr.Zero) _writeDel = (StreamWriteCallback)Marshal.GetDelegateForFunctionPointer(_native.WriteFunc, typeof(StreamWriteCallback));
            if (_native.SeekFunc != IntPtr.Zero) _seekDel = (StreamSeekCallback)Marshal.GetDelegateForFunctionPointer(_native.SeekFunc, typeof(StreamSeekCallback));
            if (_native.SetLengthFunc != IntPtr.Zero) _setLengthDel = (StreamSetLengthCallback)Marshal.GetDelegateForFunctionPointer(_native.SetLengthFunc, typeof(StreamSetLengthCallback));
            if (_native.CloseFunc != IntPtr.Zero) _closeDel = (StreamCloseCallback)Marshal.GetDelegateForFunctionPointer(_native.CloseFunc, typeof(StreamCloseCallback));
        }

        public override bool CanRead { get { return _readDel != null; } }
        public override bool CanSeek { get { return _seekDel != null; } }
        public override bool CanWrite { get { return _writeDel != null; } }

        public override long Length
        {
            get
            {
                long curr = Position;
                long len = _seekDel(_native.Context, 0, (int)SeekOrigin.End);
                Position = curr;
                return len;
            }
        }

        public override long Position
        {
            get { return _seekDel(_native.Context, 0, (int)SeekOrigin.Current); }
            set { _seekDel(_native.Context, value, (int)SeekOrigin.Begin); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = new IntPtr(h.AddrOfPinnedObject().ToInt64() + offset);
                return _readDel(_native.Context, ptr, count);
            }
            finally { h.Free(); }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = new IntPtr(h.AddrOfPinnedObject().ToInt64() + offset);
                _writeDel(_native.Context, ptr, count);
            }
            finally { h.Free(); }
        }

        public override long Seek(long offset, SeekOrigin origin) { return _seekDel(_native.Context, offset, (int)origin); }
        public override void SetLength(long value) { _setLengthDel(_native.Context, value); }
        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen && _closeDel != null)
            {
                _closeDel(_native.Context);
            }
            base.Dispose(disposing);
        }
    }
}