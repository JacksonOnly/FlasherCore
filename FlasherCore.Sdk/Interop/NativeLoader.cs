using System;
using System.Runtime.InteropServices;

namespace FlasherCore.Sdk.Interop
{
    /// <summary>
    /// 原生库加载器，用于跨平台加载和管理原生DLL
    /// </summary>
    public static class NativeLoader
    {
        /// <summary>
        /// 加载原生库
        /// </summary>
        /// <param name="path">库文件路径</param>
        /// <returns>加载成功返回库句柄，失败返回IntPtr.Zero</returns>
        public static IntPtr LoadLibrary(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path), "库文件路径不能为空");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Windows.LoadLibrary(path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Linux.dlopen(path, Linux.RTLD_NOW);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Mac.dlopen(path, Mac.RTLD_NOW);
            }

            throw new PlatformNotSupportedException("当前平台不支持原生库加载");
        }

        /// <summary>
        /// 获取库中的函数地址
        /// </summary>
        /// <param name="handle">库句柄</param>
        /// <param name="symbol">函数名称</param>
        /// <returns>函数地址，失败返回IntPtr.Zero</returns>
        public static IntPtr GetProcAddress(IntPtr handle, string symbol)
        {
            if (handle == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(handle), "库句柄不能为空");
            }

            if (string.IsNullOrEmpty(symbol))
            {
                throw new ArgumentNullException(nameof(symbol), "函数名称不能为空");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Windows.GetProcAddress(handle, symbol);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Linux.dlsym(handle, symbol);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Mac.dlsym(handle, symbol);
            }

            throw new PlatformNotSupportedException("当前平台不支持获取函数地址");
        }

        /// <summary>
        /// 释放原生库
        /// </summary>
        /// <param name="handle">库句柄</param>
        public static void FreeLibrary(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Windows.FreeLibrary(handle);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Linux.dlclose(handle);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Mac.dlclose(handle);
            }
        }

        #region 平台特定实现

        /// <summary>
        /// Windows平台特定实现
        /// </summary>
        private static class Windows
        {
            [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr LoadLibrary(string fileName);

            [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

            [DllImport("kernel32", SetLastError = true)]
            public static extern bool FreeLibrary(IntPtr hModule);
        }

        /// <summary>
        /// Linux平台特定实现
        /// </summary>
        private static class Linux
        {
            public const int RTLD_NOW = 2;
            
            [DllImport("libdl.so.2")]
            public static extern IntPtr dlopen(string fileName, int flags);
            
            [DllImport("libdl.so.2")]
            public static extern IntPtr dlsym(IntPtr handle, string symbol);
            
            [DllImport("libdl.so.2")]
            public static extern int dlclose(IntPtr handle);
        }

        /// <summary>
        /// macOS平台特定实现
        /// </summary>
        private static class Mac
        {
            public const int RTLD_NOW = 2;
            
            [DllImport("libdl.dylib")]
            public static extern IntPtr dlopen(string fileName, int flags);
            
            [DllImport("libdl.dylib")]
            public static extern IntPtr dlsym(IntPtr handle, string symbol);
            
            [DllImport("libdl.dylib")]
            public static extern int dlclose(IntPtr handle);
        }

        #endregion
    }
}
