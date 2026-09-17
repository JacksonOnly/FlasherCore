using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UPFirmwareEntry
    {
        public ushort Index;

        public fixed byte Name[128];
        public fixed byte FileName[128]; 

        public ulong Start;
        public ulong Length;
        public ulong RealLength;
        public string GetFileName()
        {
            fixed (byte* p = FileName)
            {
                var span = new ReadOnlySpan<byte>(p, 128);
                int length = span.IndexOf((byte)0);
                if (length < 0) length = 128;
                return Encoding.UTF8.GetString(span.Slice(0, length));
            }
        }
        public string GetName()
        {
            fixed (byte* p = Name)
            {
                var span = new ReadOnlySpan<byte>(p, 128);
                int length = span.IndexOf((byte)0);
                if (length < 0) length = 128;
                return Encoding.UTF8.GetString(span.Slice(0, length));
            }
        }
    }
}
