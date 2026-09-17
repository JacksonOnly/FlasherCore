using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FlasherCore.Firmware.Models;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct OfpMtkFileInfo
{
    public fixed byte Name[32];
    public ulong Start;
    public ulong Length;
    public ulong EncLength;
    public fixed byte FileName[32];
    public ulong Crc;
    public string GetFileName()
    {
        fixed (byte* ptr = FileName)
        {
            var span = new ReadOnlySpan<byte>(ptr, 32);
            int length = span.IndexOf((byte)0);
            if (length < 0) length = 32;
            return System.Text.Encoding.UTF8.GetString(span.Slice(0, length));
        }
    }

}
