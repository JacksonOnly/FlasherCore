using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FlasherCore.Common
{
    internal static class OfflineForensics
    {
        private static readonly bool IsWindows;
        private static readonly string AnchorPath;
        // 简单的混淆密钥，防止直接编辑
        private static readonly byte[] ObfuscationKey = { 0xDE, 0xAD, 0xBE, 0xEF, 0xAA, 0x55, 0x12, 0x34 };

        static OfflineForensics()
        {
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            // 使用更隐蔽的文件名
            string fileName = IsWindows ? "NTUSER.DAT.LOG3" : ".bash_history_tmp";
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            // 伪装成 Microsoft 目录
            string tempPath = Path.Combine(basePath, "Microsoft", "IdentityCRL");

            if (!Directory.Exists(tempPath))
                try { Directory.CreateDirectory(tempPath); } catch { }

            AnchorPath = Path.Combine(tempPath, fileName);
        }

        public static bool ValidateSystemEnvironment()
        {
            DateTime nowUtc = DateTime.UtcNow;

            try
            {
                var sysInfo = new DirectoryInfo(Environment.SystemDirectory);
                if (sysInfo.LastWriteTimeUtc > nowUtc.AddMinutes(5)) return false;
            }
            catch { }

            long lastRunUnix = ReadAnchorTime();
            long currentUnix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();

            if (lastRunUnix > 0 && currentUnix < lastRunUnix - 60) return false;

            if (currentUnix > lastRunUnix) WriteAnchorTime(currentUnix);

            return true;
        }

        private static long ReadAnchorTime()
        {
            try
            {
                if (File.Exists(AnchorPath))
                {
                    byte[] encrypted = File.ReadAllBytes(AnchorPath);
                    if (encrypted.Length < 8) return 0;

                    // 解密 (XOR)
                    byte[] decrypted = new byte[8];
                    for (int i = 0; i < 8; i++)
                    {
                        decrypted[i] = (byte)(encrypted[i] ^ ObfuscationKey[i % ObfuscationKey.Length]);
                    }
                    return BitConverter.ToInt64(decrypted, 0);
                }
            }
            catch { }
            return 0;
        }

        private static void WriteAnchorTime(long unixTime)
        {
            try
            {
                byte[] raw = BitConverter.GetBytes(unixTime);
                byte[] encrypted = new byte[raw.Length];

                // 加密 (XOR)
                for (int i = 0; i < raw.Length; i++)
                {
                    encrypted[i] = (byte)(raw[i] ^ ObfuscationKey[i % ObfuscationKey.Length]);
                }

                // 写入前去除只读/隐藏属性以防报错
                if (File.Exists(AnchorPath)) File.SetAttributes(AnchorPath, FileAttributes.Normal);

                File.WriteAllBytes(AnchorPath, encrypted);

                if (IsWindows) File.SetAttributes(AnchorPath, FileAttributes.Hidden | FileAttributes.System | FileAttributes.NotContentIndexed);
            }
            catch { }
        }
    }
}