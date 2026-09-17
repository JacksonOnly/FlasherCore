using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct OzipFileInfo
{
    public int Index;
    public ulong Size;        // 解压/解密后的预估大小
    public ulong CompressedSize; // ZIP 中的大小
    public OzipFileType Type;

    // Mode 2 专用：在 ZipArchive.Entries 中的索引
    // 因为 ZipArchiveEntry 是引用类型，不能放在 unsafe struct 中
    public int ZipEntryIndex;

    public bool IsEncrypted;

    // 固定缓冲区存储名称，避免大量 String 对象
    public fixed byte NameBuffer[128];
    public fixed byte FileNameBuffer[256];

    public string GetName()
    {
        fixed (byte* p = NameBuffer) return BytesToString(p, 128);
    }

    public string GetFileName()
    {
        fixed (byte* p = FileNameBuffer) return BytesToString(p, 256);
    }

    private string BytesToString(byte* ptr, int maxLen)
    {
        int len = 0;
        while (len < maxLen && ptr[len] != 0) len++;
        if (len == 0) return string.Empty;
        return Encoding.UTF8.GetString(ptr, len);
    }
}
