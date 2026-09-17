using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Sdk.Types.Firmware
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UnpackerFirmwareEntry
    {
        public ushort Index;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string FileName;

        public ulong Start;
        public ulong Length;
        public ulong RealLength;
        
    }
}
