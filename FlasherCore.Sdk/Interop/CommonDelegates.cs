using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void ProgressCallback(IntPtr userData,ulong current,ulong total);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void SpeedCallback(IntPtr userData,ulong perSecondByteCount);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate int Get_Version(
    IntPtr str,
    int length);