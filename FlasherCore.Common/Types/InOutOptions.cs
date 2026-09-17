using FlasherCore.Common.Types;
using FlasherCore.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Common.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct InOutOptions
    {
        public InOutType Type;
        public IInteropStream Stream;
        public fixed byte FileName[1024];
        public void SetFileName(string fileName)
        {
            var source = EncodingUtils.GBK.GetBytes(fileName).AsSpan();
            fixed (byte* ptr = FileName)
            {
                Span<byte> destSpan = new Span<byte>(ptr, 1024);
                destSpan.Clear();
                int copyLen = Math.Min(source.Length, 1024);
                source.Slice(0, copyLen).CopyTo(destSpan);
            }
        }
        public string GetFileName()
        {
            fixed (byte* ptr = this.FileName)
            {
                var span = new ReadOnlySpan<byte>(ptr, 1024);
                int length = span.IndexOf((byte)0);
                if (length < 0) length = 1024;

                return EncodingUtils.GBK.GetString(span.Slice(0, length));
            }
        }
    }
}
