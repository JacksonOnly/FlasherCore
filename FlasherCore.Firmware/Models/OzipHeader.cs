using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct OzipHeader
{
    // OZIP 实际上没有标准的复杂头部结构，只有 Magic
    // Mode 1: "OPPOENCRYPT!" (12 bytes)
    // Mode 2: "PK" (Zip)
    public fixed byte Magic[12];

    public OzipMode Mode;
    public long DataStartOffset;

    public string GetMagic()
    {
        fixed (byte* p = Magic) return Encoding.ASCII.GetString(p, 12).TrimEnd('\0');
    }
}