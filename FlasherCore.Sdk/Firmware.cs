using FlasherCore.Sdk.Extensions;
using FlasherCore.Sdk.Interop;
using FlasherCore.Sdk.Types;
using FlasherCore.Sdk.Types.Firmware;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static FlasherCore.Sdk.Interop.FirmwareDelegates;

namespace FlasherCore.Sdk
{
    /// <summary>
    /// 固件操作类，用于加载和操作固件文件
    /// </summary>
    public sealed class Firmware : IDisposable
    {
        #region 私有字段

        /// <summary>
        /// 原生DLL句柄
        /// </summary>
        private IntPtr _dllHandle = IntPtr.Zero;
        
        /// <summary>
        /// 会话句柄
        /// </summary>
        private IntPtr _sessionHandle = IntPtr.Zero;
        
        /// <summary>
        /// 实例句柄，用于回调
        /// </summary>
        private GCHandle _instanceHandle;
        
        /// <summary>
        /// 表示对象是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 版本信息
        /// </summary>
        private string _version;

        #region 原生函数委托
        
        private readonly FirmwareDelegates.FW_CreateSession _createSession;
        private readonly FirmwareDelegates.FW_DestorySession _destroySession;
        private readonly FirmwareDelegates.UP_SetBufferSize _setBufferSize;
        private readonly FirmwareDelegates.UP_GetEntriesCount _getEntriesCount;
        private readonly FirmwareDelegates.UP_GetEntries _getEntries;
        private readonly FirmwareDelegates.UP_GetFirmwareType _getFirmwareType;
        private readonly FirmwareDelegates.UP_SetInput _setInput;
        private readonly FirmwareDelegates.UP_SetCallback _setCallback;
        private readonly FirmwareDelegates.UP_Run _run;
        private readonly FirmwareDelegates.UP_OpenEntryStream _openEntryStream;
        private readonly Get_Version _getVersion;

        #endregion

        #region 回调委托
        
        private readonly ProgressCallback _progressCallback;
        private readonly SpeedCallback _speedCallback;
        private readonly GCHandle _progressCallbackHandle;
        private readonly GCHandle _speedCallbackHandle;

        #endregion

        #region 事件相关
        
        /// <summary>
        /// 进度事件委托
        /// </summary>
        private ProgressCallback? _progressEvent;
        
        /// <summary>
        /// 速度事件委托
        /// </summary>
        private SpeedCallback? _speedEvent;
        
        /// <summary>
        /// 事件锁，确保线程安全
        /// </summary>
        private readonly object _eventLock = new object();

        #endregion

        #endregion

        #region 公共事件

        /// <summary>
        /// 进度事件，用于报告固件操作的进度
        /// </summary>
        public event ProgressCallback? ProgressEvent
        {
            add
            {
                lock (_eventLock)
                {
                    _progressEvent += value;
                }
            }
            remove
            {
                lock (_eventLock)
                {
                    _progressEvent -= value;
                }
            }
        }

        /// <summary>
        /// 速度事件，用于报告固件操作的速度
        /// </summary>
        public event SpeedCallback? SpeedEvent
        {
            add
            {
                lock (_eventLock)
                {
                    _speedEvent += value;
                }
            }
            remove
            {
                lock (_eventLock)
                {
                    _speedEvent -= value;
                }
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化Firmware类的新实例
        /// </summary>
        /// <param name="dllPath">原生DLL路径</param>
        /// <param name="helper">安全助手，用于生成签名</param>
        public unsafe Firmware(string dllPath)
        {
            if (string.IsNullOrEmpty(dllPath))
            {
                throw new ArgumentNullException(nameof(dllPath), "DLL路径不能为空");
            }

            // 加载原生DLL
            _dllHandle = NativeLoader.LoadLibrary(dllPath);
            if (_dllHandle == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new FileNotFoundException(
                    $"加载原生库失败，路径：{dllPath}，错误码：{errorCode}",
                    dllPath
                );
            }

            try
            {
                // 加载DLL中的函数
                _createSession = LoadNativeFunction<FirmwareDelegates.FW_CreateSession>("FW_CreateSession");
                _destroySession = LoadNativeFunction<FirmwareDelegates.FW_DestorySession>("FW_DestorySession");
                _setBufferSize = LoadNativeFunction<FirmwareDelegates.UP_SetBufferSize>("UP_SetBufferSize");
                _getEntriesCount = LoadNativeFunction<FirmwareDelegates.UP_GetEntriesCount>("UP_GetEntriesCount");
                _getEntries = LoadNativeFunction<FirmwareDelegates.UP_GetEntries>("UP_GetEntries");
                _getFirmwareType = LoadNativeFunction<FirmwareDelegates.UP_GetFirmwareType>("UP_GetFirmwareType");
                _setInput = LoadNativeFunction<FirmwareDelegates.UP_SetInput>("UP_SetInput");
                _setCallback = LoadNativeFunction<FirmwareDelegates.UP_SetCallback>("UP_SetCallback");
                _run = LoadNativeFunction<FirmwareDelegates.UP_Run>("UP_Run");
                _openEntryStream = LoadNativeFunction<UP_OpenEntryStream>("UP_OpenEntryStream");
                _getVersion = LoadNativeFunction<Get_Version>("Get_Version");
            }
            catch (Exception ex)
            {
                // 加载函数失败，释放资源
                NativeLoader.FreeLibrary(_dllHandle);
                _dllHandle = IntPtr.Zero;
                throw new InvalidOperationException(
                    $"从DLL加载函数失败：{ex.Message}", ex);
            }

            _sessionHandle = _createSession();

            // 检查会话创建结果
            if ((long)_sessionHandle < 10000)
            {
                int errCode = (int)_sessionHandle;
                var err = (FlasherCoreResult)errCode;
                NativeLoader.FreeLibrary(_dllHandle);
                _dllHandle = IntPtr.Zero;
                throw new InvalidOperationException(
                    $"创建原生会话失败。错误：{err}，错误码：{errCode}"
                );
            }

            GetVersion();

            // 初始化回调
            _instanceHandle = GCHandle.Alloc(this, GCHandleType.Normal);

            _progressCallback = OnProgressInternal;
            _speedCallback = OnSpeedInternal;

            _progressCallbackHandle = GCHandle.Alloc(_progressCallback, GCHandleType.Normal);
            _speedCallbackHandle = GCHandle.Alloc(_speedCallback, GCHandleType.Normal);

            // 设置回调
            SetCallbackInternal();

            _disposed = false;

            Debug.WriteLine($"固件操作类初始化成功。会话：0x{_sessionHandle:X}");
        }

        #endregion

        #region 回调实现

        /// <summary>
        /// 设置回调函数
        /// </summary>
        private void SetCallbackInternal()
        {
            IntPtr instancePtr = GCHandle.ToIntPtr(_instanceHandle);

            var result = _setCallback(_sessionHandle, instancePtr, _progressCallback, _speedCallback);

            if (result != 0)
            {
                throw new InvalidOperationException(
                    $"设置回调失败。错误：{(FlasherCoreResult)result}"
                );
            }

            Debug.WriteLine("回调设置成功");
        }

        /// <summary>
        /// 进度回调内部处理
        /// </summary>
        /// <param name="userData">用户数据指针</param>
        /// <param name="current">当前进度</param>
        /// <param name="total">总进度</param>
        private static void OnProgressInternal(IntPtr userData, ulong current, ulong total)
        {
            try
            {
                if (userData == IntPtr.Zero)
                    return;

                var handle = GCHandle.FromIntPtr(userData);
                if (!handle.IsAllocated)
                    return;

                var firmware = handle.Target as Firmware;
                if (firmware == null || firmware._disposed)
                    return;

                firmware.OnProgress(current, total);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"进度回调错误：{ex.Message}");
            }
        }

        /// <summary>
        /// 速度回调内部处理
        /// </summary>
        /// <param name="userData">用户数据指针</param>
        /// <param name="perMsByteCount">每秒字节数</param>
        private static void OnSpeedInternal(IntPtr userData, ulong perMsByteCount)
        {
            try
            {
                if (userData == IntPtr.Zero)
                    return;

                var handle = GCHandle.FromIntPtr(userData);
                if (!handle.IsAllocated)
                    return;

                var firmware = handle.Target as Firmware;
                if (firmware == null || firmware._disposed)
                    return;

                firmware.OnSpeed(perMsByteCount);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"速度回调错误：{ex.Message}");
            }
        }

        /// <summary>
        /// 触发进度事件
        /// </summary>
        /// <param name="current">当前进度</param>
        /// <param name="total">总进度</param>
        private void OnProgress(ulong current, ulong total)
        {
            ProgressCallback? handler;
            lock (_eventLock)
            {
                handler = _progressEvent;
            }

            if (handler != null)
            {
                try
                {
                    handler(GCHandle.ToIntPtr(_instanceHandle), current, total);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"进度事件处理程序错误：{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 触发速度事件
        /// </summary>
        /// <param name="perMsByteCount">每秒字节数</param>
        private void OnSpeed(ulong perMsByteCount)
        {
            SpeedCallback? handler;
            lock (_eventLock)
            {
                handler = _speedEvent;
            }

            if (handler != null)
            {
                try
                {
                    handler(GCHandle.ToIntPtr(_instanceHandle), perMsByteCount);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"速度事件处理程序错误：{ex.Message}");
                }
            }
        }

        #endregion

        #region 公共方法
        public string Version => _version;

        /// <summary>
        /// 设置缓冲区大小
        /// </summary>
        /// <param name="size">缓冲区大小，单位为字节</param>
        public void SetBufferSize(uint size)
        {
            ThrowIfDisposed();
            var result = _setBufferSize(_sessionHandle, size);
            CheckSecurityResult(result, nameof(SetBufferSize));
        }

        /// <summary>
        /// 设置固件文件名
        /// </summary>
        /// <param name="filePath">固件文件路径</param>
        public void SetInput(InOutOptions inOpt)
        {
            ThrowIfDisposed();
            
            int result = _setInput(_sessionHandle, ref inOpt);
            if (result != 0)
            {
                throw new InvalidOperationException(
                    $"设置Input失败。错误：{(FlasherCoreResult)result}"
                );
            }
        }

        /// <summary>
        /// 获取固件类型
        /// </summary>
        /// <returns>固件类型</returns>
        public UnpackerFirmwareType GetFirmwareType()
        {
            ThrowIfDisposed();
            var result = _getFirmwareType(_sessionHandle, out var type);
            CheckSecurityResult(result, nameof(GetFirmwareType));
            return type;
        }

        /// <summary>
        /// 获取固件条目
        /// </summary>
        /// <returns>固件条目数组</returns>
        public UnpackerFirmwareEntry[] GetEntries()
        {
            ThrowIfDisposed();

            int ret = _getEntriesCount(_sessionHandle, out int count);
            if (ret != 0)
            {
                throw new InvalidOperationException(
                    $"获取条目计数失败。错误码：{(FlasherCoreResult)ret}"
                );
            }

            if (count <= 0)
            {
                return Array.Empty<UnpackerFirmwareEntry>();
            }

            var entries = new UnpackerFirmwareEntry[count];
            ret = _getEntries(_sessionHandle, entries, count);
            if (ret != 0)
            {
                throw new InvalidOperationException(
                    $"获取条目失败。错误码：{(FlasherCoreResult)ret}"
                );
            }

            return entries;
        }

        /// <summary>
        /// 运行固件操作
        /// </summary>
        /// <param name="options">运行选项</param>
        /// <returns>操作结果</returns>
        public int Run(ushort id,InOutOptions options)
        {
            ThrowIfDisposed();

            GC.KeepAlive(_progressCallback);
            GC.KeepAlive(_speedCallback);

            try
            {
                return _run(_sessionHandle,id, ref options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"运行固件操作失败：{ex.Message}");
                return (int)FlasherCoreResult.UnknownError;
            }
        }
        
        public int OpenEntryStream(ushort id,out Stream? stream)
        {
            ThrowIfDisposed();
            stream = null;
            try
            {
                var result =  _openEntryStream(_sessionHandle, id, out var inOut);
                if(result == (int)FlasherCoreResult.Success)
                {
                    stream = inOut.Stream.AsStream(false);
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"打开流失败：{ex.Message}");
                return (int)FlasherCoreResult.UnknownError;
            }
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 获取版本信息
        /// </summary>
        private void GetVersion()
        {
            var ptr = Marshal.AllocHGlobal(2048);
            try
            {
                var count = _getVersion(ptr, 2048);
                if (count > 0)
                {
                    _version = Marshal.PtrToStringAuto(ptr, count);

                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

        }

        /// <summary>
        /// 从DLL加载原生函数
        /// </summary>
        /// <typeparam name="T">委托类型</typeparam>
        /// <param name="functionName">函数名</param>
        /// <returns>委托实例</returns>
        private T LoadNativeFunction<T>(string functionName) where T : Delegate
        {
            IntPtr ptr = NativeLoader.GetProcAddress(_dllHandle, functionName);
            if (ptr == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException(
                    $"在加载的库中找不到函数 '{functionName}'。"
                );
            }

            try
            {
                return Marshal.GetDelegateForFunctionPointer<T>(ptr);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"为函数 '{functionName}' 创建委托失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查安全结果
        /// </summary>
        /// <param name="result">结果代码</param>
        /// <param name="method">调用方法名</param>
        private void CheckSecurityResult(FlasherCoreResult result, string method)
        {
            if (result != FlasherCoreResult.Success)
            {
                throw new InvalidOperationException($"{method} 失败。安全结果：{result}");
            }
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Firmware));
            }
        }

        #endregion

        #region 释放资源

        /// <summary>
        /// 释放对象占用的资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 释放资源的内部实现
        /// </summary>
        /// <param name="disposing">是否为显式释放</param>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Debug.WriteLine($"释放Firmware资源 (disposing: {disposing})");

                // 释放会话
                if (_sessionHandle != IntPtr.Zero && _destroySession != null)
                {
                    try
                    {
                        _destroySession(_sessionHandle);
                        Debug.WriteLine("原生会话已销毁");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"销毁会话时出错：{ex.Message}");
                    }
                    _sessionHandle = IntPtr.Zero;
                }

                // 释放回调委托句柄
                if (_progressCallbackHandle.IsAllocated)
                {
                    _progressCallbackHandle.Free();
                }
                if (_speedCallbackHandle.IsAllocated)
                {
                    _speedCallbackHandle.Free();
                }

                // 释放实例句柄
                if (_instanceHandle.IsAllocated)
                {
                    _instanceHandle.Free();
                }

                // 释放DLL句柄
                if (_dllHandle != IntPtr.Zero)
                {
                    try
                    {
                        NativeLoader.FreeLibrary(_dllHandle);
                        Debug.WriteLine("DLL已卸载");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"卸载DLL时出错：{ex.Message}");
                    }
                    _dllHandle = IntPtr.Zero;
                }

                // 清除事件
                lock (_eventLock)
                {
                    _progressEvent = null;
                    _speedEvent = null;
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~Firmware()
        {
            Dispose(false);
        }

        #endregion
    }
}