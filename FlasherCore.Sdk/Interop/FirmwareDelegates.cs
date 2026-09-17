using FlasherCore.Sdk.Types;
using FlasherCore.Sdk.Types.Firmware;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Sdk.Interop
{
    internal static class FirmwareDelegates
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr FW_CreateSession();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FW_DestorySession(
            IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate FlasherCoreResult UP_SetBufferSize(
            IntPtr handle,
            uint bufferSize);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int UP_GetEntriesCount(
            IntPtr handle,
            out int count); 
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int UP_GetEntries(
            IntPtr handle,
            [In, Out] UnpackerFirmwareEntry[] entries,
            int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate FlasherCoreResult UP_GetFirmwareType(
            IntPtr handle,
            out UnpackerFirmwareType type);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int UP_SetInput(
            IntPtr handle,
           ref InOutOptions runOptions);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int UP_SetCallback(
    IntPtr handle,
    IntPtr userData,
     ProgressCallback progressCallback,
     SpeedCallback speedCallback);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int UP_Run(
            IntPtr handle,
            ushort id,
            ref InOutOptions runOptions);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int UP_OpenEntryStream(
    IntPtr handle,
    ushort id,
    out InOutOptions runOptions);
    }
}
