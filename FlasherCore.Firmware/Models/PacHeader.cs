using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct PacHeader
{
    public fixed byte szVersion[44];
    public uint dwHiSize;
    public uint dwLoSize;
    public fixed byte szPrdName[512];
    public fixed byte szPrdVersion[512];
    public uint nFileCount;
    public uint dwFileOffset;
    public uint dwMode;
    public uint dwFlashType;
    public uint dwNandStrategy;
    public uint dwIsNvBackup;
    public uint dwNandPageType;
    public fixed byte szPrdAlias[200];
    public uint dwOmaDmProductFlag;
    public uint dwIsOmaDM;
    public uint dwIsPreload;
    public fixed byte dwReserved[800];
    public uint dwMagic;
    public ushort wCRC1;
    public ushort wCRC2;

    public string GetVersion()
    {
        fixed (byte* p = szVersion) return GetString(p, 44);
    }

    public string GetPrdName()
    {
        fixed (byte* p = szPrdName) return GetString(p, 512);
    }

    public string GetPrdVersion()
    {
        fixed (byte* p = szPrdVersion) return GetString(p, 512);
    }

    public string GetPrdAlias()
    {
        fixed (byte* p = szPrdAlias) return GetString(p, 200);
    }

    private string GetString(byte* ptr, int maxLength)
    {
        int len = 0;
        // UTF-16LE 字符串，寻找双字节 0x0000 结尾
        for (; len < maxLength; len += 2)
        {
            if (ptr[len] == 0 && ptr[len + 1] == 0) break;
        }
        if (len == 0) return string.Empty;
        return Encoding.Unicode.GetString(ptr, len);
    }
}