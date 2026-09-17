using FlasherCore.Common;
using FlasherCore.Common.Types;
using FlasherCore.Firmware.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Firmware
{
    public class FWSession : BaseSession, IDisposable
    {
        public IInteropStream InputStream;
        public string? InputFileName;
        public InOutType InputType;

        public uint BufferSize = 2*1024*1024;


        public UPFirmwareType FirmwareType;


        public IntPtr UserData;
        public ProgressCallback? Progress;
        public SpeedCallback? Speed;


        public UnpackerTool? Tool { get; private set; }
        public FlasherCoreResult InitializeTool()
        {
            if (Tool == null)
            {
                Tool = new UnpackerTool(this);
            }

            return Tool.InitValues();

        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Tool?.Dispose();
                Tool = null;
            }
            base.Dispose(disposing);
        }
    }
}
