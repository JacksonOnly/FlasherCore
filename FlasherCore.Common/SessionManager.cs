using FlasherCore.Common.Helpers;
using FlasherCore.Common.Types;
using FlasherCore.Common.Utilities;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;

namespace FlasherCore.Common
{
    public static class SessionManager<T> where T : BaseSession, new()
    {

        private static readonly ConcurrentDictionary<IntPtr, T> _sessions = new();
        private const int MIN_HANDLE = 10000;
        private static long _nextHandleId = MIN_HANDLE; 


        public static FlasherCoreResult CreateSession( out IntPtr handle)
        {
            handle = IntPtr.Zero;
            try
            {
                var session = new T
                {
                };

                long newId = Interlocked.Increment(ref _nextHandleId);
                handle = new IntPtr(newId);
                PipeLogger.Info("Session Created: 0x{0:X}", [handle]);

                if (_sessions.TryAdd(handle, session))
                {
                    return FlasherCoreResult.Success;
                }
            }
            catch (Exception ex)
            {
                PipeLogger.Error("Auth Error: {0}", [ex]);
                return FlasherCoreResult.UnknownError;
            }

            return FlasherCoreResult.UnknownError;
        }
        public static FlasherCoreResult GetSession(IntPtr handle, out T? session)
        {
            session = null;
            if (!_sessions.TryGetValue(handle, out session))
                return FlasherCoreResult.SessionInvalid;
            return FlasherCoreResult.Success;
        }
        public static void DestroySession(IntPtr handle)
        {
            if (_sessions.TryRemove(handle, out var s))
            {
                s.Dispose();
            }
        }
    }
}