using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Common.Utilities
{
    public static class DllPathHelper
    {
        // 1. 获取当前线程的调用堆栈，拿到“返回地址”
        // FramesToSkip = 0 表示获取当前方法的地址，1 表示调用者的地址
        [DllImport("kernel32.dll")]
        private static extern ushort RtlCaptureStackBackTrace(
            uint FramesToSkip,
            uint FramesToCapture,
            out IntPtr BackTrace,
            out uint BackTraceHash
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetModuleHandleEx(
            uint dwFlags,
            IntPtr lpModuleName,
            out IntPtr phModule
        );

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetModuleFileName(
            IntPtr hModule,
            StringBuilder lpFilename,
            int nSize
        );

        private const uint GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;
        private const uint GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT = 0x00000002;

        public static string GetCurrentDllPath()
        {
            try
            {
                IntPtr ptr;
                uint hash;
                RtlCaptureStackBackTrace(0, 1, out ptr, out hash);

                if (ptr == IntPtr.Zero)
                    return string.Empty;
                if (GetModuleHandleEx(
                    GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                    ptr,
                    out IntPtr hModule))
                {
                    StringBuilder sb = new StringBuilder(1024);
                    if (GetModuleFileName(hModule, sb, sb.Capacity) > 0)
                    {
                        return sb.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            return string.Empty;
        }
    }
}