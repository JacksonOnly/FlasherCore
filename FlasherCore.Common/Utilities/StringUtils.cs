using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Common.Utilities
{
    public static class StringUtils
    {
        public static Span<byte> Utf8(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public static int TryUtf8(this string str, Span<byte> dest)
        {
            Encoding.UTF8.TryGetBytes(str, dest, out var len);
            return len;
        }

        public static bool IsUrl(this string str)
        {
            if (Uri.TryCreate(str, UriKind.Absolute, out var uri))
            {
                switch (uri.Scheme)
                {
                    case "http":
                    case "https":
                        return true;
                    default:
                        return false;
                }
            }
            return false;
        }

        public static bool IsFile(this string str)
        {
            if (Uri.TryCreate(str, UriKind.Absolute, out var uri))
            {
                return uri.IsFile;
            }
            return false;
        }
    }
}
