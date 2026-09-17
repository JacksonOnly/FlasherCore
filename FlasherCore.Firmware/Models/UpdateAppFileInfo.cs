using FlasherCore.Common.Utilities;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct UpdateAppFileInfo
{
    public uint HeaderId;       // Magic: 0xA55AAA55
    public uint HeaderSize;     // Offset to Data
    public uint Unknown1;
    public fixed byte HardwareId[8];
    public uint FileSequence;
    public uint FileSize;       // Data Length
    public fixed byte FileDate[16];
    public fixed byte FileTime[16];
    public fixed byte FileType[16];
    public fixed byte Blank1[16];
    public ushort HeaderChecksum;
    public ushort BlockSize;
    public ushort Blank2;

    public string GetHardwareId()
    {
        fixed (byte* ptr = HardwareId) return UnsafeUtils.GetString(ptr, 8);
    }

    public string GetFileDate()
    {
        fixed (byte* ptr = FileDate) return UnsafeUtils.GetString(ptr, 16);
    }

    public string GetFileTime()
    {
        fixed (byte* ptr = FileTime) return UnsafeUtils.GetString(ptr, 16);
    }

    public string GetFileType()
    {
        fixed (byte* ptr = FileType) return UnsafeUtils.GetString(ptr, 16);
    }


}