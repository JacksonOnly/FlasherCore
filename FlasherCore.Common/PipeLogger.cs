using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using FlasherCore.Common.Utilities;

namespace FlasherCore.Common
{
    public static class PipeLogger
    {
        private static NamedPipeClientStream _loggerClient;
        private static readonly object _sendLock = new object();
        static PipeLogger()
        {
            _loggerClient = new NamedPipeClientStream(".","FlasherCore.Logger",PipeDirection.Out);
        }

        private static void TryConnect()
        {
            try
            {
                _loggerClient.Connect(500);
            }
            catch (TimeoutException)
            { }
        }
        #region 日志功能

        private static void WriteLine(string prefix, string message, string file, int line, string member)
        {
            if (_loggerClient == null) return;
            if(!_loggerClient.IsConnected)
            {
                TryConnect();
            }
            string logLine = $"[{DateTime.Now:HH:mm:ss.fff}]{prefix} {Path.GetFileName(file)}:{line} ({member}) -> {message}";

            lock (_sendLock)
            {
                try
                {
                    _loggerClient.Write(logLine.Utf8());
                    _loggerClient.Flush();
                }
                catch { }
            }
        }

        public static void Debug(string format, object[]? args = null, [CallerMemberName] string name = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = -1)
            => ProcessLog("[DBG]", format, args, name, filePath, line);

        public static void Info(string format, object[]? args = null, [CallerMemberName] string name = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = -1)
            => ProcessLog("[INF]", format, args, name, filePath, line);

        public static void Warn(string format, object[]? args = null, [CallerMemberName] string name = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = -1)
            => ProcessLog("[WRN]", format, args, name, filePath, line);

        public static void Error(string format, object[]? args = null, [CallerMemberName] string name = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = -1)
            => ProcessLog("[ERR]", format, args, name, filePath, line);

        private static void ProcessLog(string prefix, string format, object[]? args, string name, string filePath, int line)
        {
            string msg;
            try { msg = (args != null && args.Length > 0) ? string.Format(format, args) : format; }
            catch (Exception ex) { msg = $"{format} [FormatError: {ex.Message}]"; }
            WriteLine(prefix, msg, filePath, line, name);
        }
        #endregion
    }
}