using System;
using System.Security.Cryptography;

namespace FlasherCore.Common.Utilities
{
    public static class CryptoShared
    {
        /// <summary>
        /// AES-GCM 解密
        /// </summary>
        public static byte[] DecryptAesGcm(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag)
        {
            using (var aes = new AesGcm(key,16))
            {
                var plaintext = new byte[ciphertext.Length];
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
                return plaintext;
            }
        }

        /// <summary>
        /// RSA 验签 (SHA256)
        /// </summary>
        public static bool VerifyRsaSignature(byte[] data, ReadOnlySpan<byte>  signature, string publicKeyXml)
        {
            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.FromXmlString(publicKeyXml);
                    return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}