using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Common.Utilities
{
    public static unsafe class UnsafeUtils
    {
        public static string GetString(byte* ptr, int maxLength)
        {
            int len = 0;
            while (len < maxLength && ptr[len] != 0) len++;
            if (len == 0) return string.Empty;
            return Encoding.ASCII.GetString(ptr, len);
        }
        public static string GetString(this IntPtr ptr, int maxLength)
        {
            return new string((sbyte*)ptr, 0, maxLength, Encoding.ASCII).TrimEnd('\0');
        }
        public static string ToFixedString(byte* ptr, int length)
        {
            int actualLen = 0;
            while (actualLen < length && ptr[actualLen] != 0)
            {
                actualLen++;
            }
            return new string((sbyte*)ptr, 0, actualLen, Encoding.ASCII);
        }
        public static void CopyToPtr(this string s, byte* ptr, int ptrLength)
        {
            if (string.IsNullOrEmpty(s)) return;
            var srcSpan = s.AsSpan();
            var destSpan = new Span<byte>(ptr, ptrLength);
            destSpan.Clear();
            Encoding.UTF8.GetBytes(srcSpan, destSpan);
        }
        public static void CopyStringBytes(byte* src, int srcLenBytes, byte* dest, int destLenBytes)
        {
            new Span<byte>(dest, destLenBytes).Clear();
            int len = 0;
            for (; len < srcLenBytes; len += 2)
            {
                if (src[len] == 0 && src[len + 1] == 0) break;
            }
            if (len == 0) return;
            ReadOnlySpan<char> charSpan = new ReadOnlySpan<char>(src, len / 2);
            Span<byte> destSpan = new Span<byte>(dest, destLenBytes);
            Encoding.UTF8.GetBytes(charSpan, destSpan);
        }
    }
}
