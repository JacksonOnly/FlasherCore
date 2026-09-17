using FlasherCore.Common;
using FlasherCore.Common.Types;
using FlasherCore.Firmware.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware
{
    public unsafe class Exports
    {
        static Exports()
        {
        }
        private static bool Verify(IntPtr handle, out FlasherCoreResult result, out FWSession session)
        {
            FWSession? s;
            var verifyResult = SessionManager<FWSession>.GetSession(handle, out s);
            result = verifyResult;
            if (s != null)
                session = s;
            else
                session = new FWSession();

            return verifyResult == FlasherCoreResult.Success && session != null;
        }
        [UnmanagedCallersOnly(EntryPoint = "Get_Version",CallConvs = [typeof(CallConvCdecl)])]
        public static int GetLicense(char* str,int count)
        {
            var span = new Span<char>(str, count);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[Version]: 1.3.0_Firmware");
            //sb.AppendLine("[License]: " + SessionManager<FWSession>.License);
            //sb.AppendLine("[Expire]: " + DateTimeOffset.FromUnixTimeSeconds(SessionManager<FWSession>.Expire).LocalDateTime.ToString());
            sb.CopyTo(0,span, sb.Length);
            return sb.Length;
        }
        [UnmanagedCallersOnly(EntryPoint = "UP_GetEntries",CallConvs = [typeof(CallConvCdecl)])]
        public static int UP_GetEntries(IntPtr handle, UPFirmwareEntry* entries, int length)
        {
            if (!Verify(handle, out var verifyResult, out var session)) return (int)verifyResult;

            try
            {
                if (!EnsureToolInitialized(session, out var result)) return result;
                return (int)session.Tool!.GetEntries(entries, length);
            }
            catch (Exception)
            {
                return (int)FlasherCoreResult.UnknownError;
            }
        }

        [UnmanagedCallersOnly(EntryPoint = "UP_GetEntriesCount", CallConvs = [typeof(CallConvCdecl)])]
        public static int UP_GetEntriesCount(IntPtr handle, int* count)
        {
            if (!Verify(handle, out var verifyResult, out var session)) return (int)verifyResult;

            try
            {
                if (!EnsureToolInitialized(session, out var result)) return result;
                *count = session.Tool!.GetEntryCount();
                return (int)FlasherCoreResult.Success;
            }
            catch
            {
                return (int)FlasherCoreResult.UnknownError;
            }
        }
        [UnmanagedCallersOnly(EntryPoint ="UP_OpenEntryStream", CallConvs = [typeof(CallConvCdecl)])]
        public static int UP_OpenEntryStream(IntPtr handle,ushort id,InOutOptions* runOptions)
        {
            if (!Verify(handle, out var verifyResult, out var session)) return (int)verifyResult;
            if (!EnsureToolInitialized(session, out var result)) return result;
            return (int)session.Tool!.OpenEntryStream(id, runOptions);
        }
        [UnmanagedCallersOnly(EntryPoint = "UP_Run", CallConvs = [typeof(CallConvCdecl)])]
        public static int UP_Run(IntPtr handle,ushort id, IntPtr runOptionsPtr)
        {
            if (!Verify(handle, out var verifyResult, out var session)) return (int)verifyResult;
            if (!EnsureToolInitialized(session, out var result)) return result;
            return (int)session.Tool!.Run(id,(InOutOptions*)runOptionsPtr);
        }
        private static bool EnsureToolInitialized(FWSession session,out int result)
        {
            result = 0;
            try
            {
                var ret = session.InitializeTool();
                if (ret != FlasherCoreResult.Success)
                {
                    result = (int)ret;
                    return false;
                }
            }
            catch (IOException ioErr)
            {
                result =(int) FlasherCoreResult.IOException;
            }
            catch (Exception ex)
            {
                result = (int)FlasherCoreResult.UnknownError;
            }
            return true;
        }
        [UnmanagedCallersOnly(EntryPoint = "UP_SetBufferSize", CallConvs = [typeof(CallConvCdecl)])]
        public static FlasherCoreResult UP_SetBufferSize(IntPtr handle,uint bufferSize)
        {
            if (!Verify(handle, out var verifyResult, out var session))
            {
                return verifyResult;
            }
            session.BufferSize = bufferSize;
            return verifyResult;
        }
        [UnmanagedCallersOnly(EntryPoint = "UP_SetInput", CallConvs = [typeof(CallConvCdecl)])]
        public static int UP_SetInput(IntPtr handle, IntPtr optionsPtr)
        {
            try
            {
                if (optionsPtr == IntPtr.Zero)
                {
                    return (int)FlasherCoreResult.InvalidPointer;
                }
                if (!Verify(handle, out var verifyResult, out var session))
                {
                    return (int)verifyResult;
                }
                var option = (InOutOptions*)optionsPtr;
                session.InputType = option->Type;
                if (session.InputType == InOutType.File)
                    session.InputFileName = option->GetFileName();
                else
                {
                    if (option->Stream.ReadFunc == IntPtr.Zero)
                        return (int)FlasherCoreResult.InvalidPointer;
                    session.InputStream = option->Stream;
                }
                Debug.WriteLine(session.InputFileName);
                return (int)FlasherCoreResult.Success;
            }
            catch (Exception ex)
            {
                PipeLogger.Error($"[NativeCrashPrevent] Error: {ex.Message}", []);
                return (int)FlasherCoreResult.UnknownError;
            }
        }
        [UnmanagedCallersOnly(EntryPoint = "UP_GetFirmwareType", CallConvs = [typeof(CallConvCdecl)])]
        public static FlasherCoreResult UP_GetFirmwareType(IntPtr handle, UPFirmwareType* type)
        {
            if (!Verify(handle, out var verifyResult, out var session))
            {
                return verifyResult;
            }
            *type = session.FirmwareType;
            return verifyResult;
        }
        [UnmanagedCallersOnly(EntryPoint = "UP_SetCallback", CallConvs = [typeof(CallConvCdecl)])]
        public static FlasherCoreResult UP_SetCallback(IntPtr handle, IntPtr userData, IntPtr progressCallback,IntPtr speedCallback)
        {
            if (!Verify(handle, out var verifyResult, out var session))
            {
                return verifyResult;
            }
            session.UserData = userData;
            session.Progress = Marshal.GetDelegateForFunctionPointer<ProgressCallback>(progressCallback);
            session.Speed = Marshal.GetDelegateForFunctionPointer<SpeedCallback>(speedCallback);
            return verifyResult;
        }
        [UnmanagedCallersOnly(EntryPoint = "FW_CreateSession", CallConvs = [typeof(CallConvCdecl)])]
        public unsafe static IntPtr FW_CreateSession()
        {
            return CreateSession();
        }

        [UnmanagedCallersOnly(EntryPoint = "FW_DestorySession", CallConvs = [typeof(CallConvCdecl)])]
        public static void FW_DestorySession(IntPtr handle)
        {
            DestorySession(handle);
        }
        private static IntPtr CreateSession()
        {
            var result = SessionManager<FWSession>.CreateSession( out var handle);
            if (result == FlasherCoreResult.Success)
                return handle;
            else
                return (IntPtr)result;
        }
        private static void DestorySession(IntPtr handle)
        {
            SessionManager<FWSession>.DestroySession(handle);
        }
    }
}
