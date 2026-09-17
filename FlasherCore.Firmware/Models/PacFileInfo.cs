using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct PacFileInfo
{
    public uint dwSize;
    public fixed byte szFileID[512];
    public fixed byte szFileName[512];
    public fixed byte szFileVersion[504];
    public uint dwHiFileSize;
    public uint dwHiDataOffset;
    public uint dwLoFileSize;
    public uint nFileFlag;
    public uint nCheckFlag;
    public uint dwLoDataOffset;
    public uint dwCanOmitFlag;
    public uint dwAddrNum;

    public fixed byte Reserved[1016];

    public string GetFileID()
    {
        fixed (byte* p = szFileID) return GetString(p, 512);
    }

    public string GetFileName()
    {
        fixed (byte* p = szFileName) return GetString(p, 512);
    }

    public string GetFileVersion()
    {
        fixed (byte* p = szFileVersion) return GetString(p, 504);
    }

    public long GetFileSize() => ((long)dwHiFileSize << 32) | dwLoFileSize;
    public long GetDataOffset() => ((long)dwHiDataOffset << 32) | dwLoDataOffset;

    private string GetString(byte* ptr, int maxLength)
    {
        int len = 0;
        for (; len < maxLength; len += 2)
        {
            if (ptr[len] == 0 && ptr[len + 1] == 0) break;
        }
        if (len == 0) return string.Empty;
        return Encoding.Unicode.GetString(ptr, len);
    }
}