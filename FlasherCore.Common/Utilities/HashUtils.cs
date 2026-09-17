using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FlasherCore.Common.Utilities
{
    public static class HashUtils
    {
        public static string HmacSha256Hex(string data, string key, bool lower = false)
            => HmacSha256Hex(Encoding.UTF8.GetBytes(data),Encoding.UTF8.GetBytes(key),lower);
        public static string HmacSha256Hex(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key,bool lower =false)
        {
            return lower ? HmacSha256(data, key).ToHexStringLower() : HmacSha256(data, key).ToHexString();
        }
        public static ReadOnlySpan<byte> HmacSha256(ReadOnlySpan<byte>data,ReadOnlySpan<byte> key)
        {
            return HMACSHA256.HashData(key, data);
        }
        public static ReadOnlySpan<byte> Md5(ReadOnlySpan<byte>data)
        {
            return MD5.HashData(data);
        }
    }
}
