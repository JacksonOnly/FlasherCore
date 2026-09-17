using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Sdk.Types
{
    [StructLayout(LayoutKind.Sequential,Pack =1)]
    public unsafe struct InOutOptions
    {
        public InOutType Type;
        public IInteropStream Stream;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst =1024)]
        public string FileName;
       
    }
}
