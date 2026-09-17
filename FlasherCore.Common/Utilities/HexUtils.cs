using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Common.Utilities
{
    public static class HexUtils
    {
        public static string ToHexStringLower(this ReadOnlySpan<byte> bytes)
        {
            return Convert.ToHexStringLower(bytes);
        }
        public static string ToHexString(this ReadOnlySpan<byte> bytes)
        {
            return Convert.ToHexString(bytes);
        }
        public static Span<byte> HexToByteArray(this string hex)
        {
            return Convert.FromHexString(hex);
        }
    }
}
