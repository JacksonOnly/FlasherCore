using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Common.Utilities
{
    public static class Base64Utils
    {
        public static ReadOnlySpan<byte> Decode(string str)
        {
            return Convert.FromBase64String(str);
        }
        public static string Encode(ReadOnlySpan<byte> bytes)
        {
            return Convert.ToBase64String(bytes); 
        }
        public static string DecodeString(string str)
        {
            return Decode(str).Utf8();
        }
        public static string EncodeString(string str)
        {
            return Encode(str.Utf8());
        }
    }
}
