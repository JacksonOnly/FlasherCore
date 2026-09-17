using System;

namespace FlasherCore.Common.Utilities;

public static class FormatExtensions
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

    /// <summary>
    /// 将字节数转换为人类可读的文件大小格式 (例如: 1.25 MB)
    /// </summary>
    public static string ToSizeString(this ulong bytes, int decimalPlaces = 2)
    {
        if (bytes == 0) return "0 B";

        // 这里的 1024 是计算机标准。如果你想用硬盘厂商标准(1000)，请改为 1000
        const int scale = 1024;

        // 计算层级 (0=B, 1=KB, 2=MB...)
        // 使用 Math.Log 计算它是 1024 的几次方
        int order = (int)(Math.Log(bytes) / Math.Log(scale));

        // 防止超出数组范围 (虽然 ulong 最大也就到 EB 级别)
        if (order >= SizeSuffixes.Length) order = SizeSuffixes.Length - 1;

        // 计算显示数值
        double adjustedSize = bytes / Math.Pow(scale, order);

        // 格式化输出
        // N{decimalPlaces} 表示保留几位小数
        return $"{adjustedSize.ToString($"N{decimalPlaces}")} {SizeSuffixes[order]}";
    }

    /// <summary>
    /// 专门用于显示速度的扩展 (例如: 5.2 MB/s)
    /// </summary>
    public static string ToSpeedString(this ulong bytesPerSecond)
    {
        return $"{bytesPerSecond.ToSizeString()}/s";
    }

    // 重载支持 long 类型 (以防万一你用 long)
    public static string ToSizeString(this long bytes, int decimalPlaces = 2)
    {
        if (bytes < 0) return "-" + ToSizeString((ulong)-bytes, decimalPlaces);
        return ToSizeString((ulong)bytes, decimalPlaces);
    }
}