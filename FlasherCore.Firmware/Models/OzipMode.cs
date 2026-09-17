using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Firmware.Models;

public enum OzipMode : int
{
    Unknown = 0,
    OzipPayload = 1, // Mode 1: Whole file encrypted
    ZipContainer = 2 // Mode 2: Zip with internal encrypted files
}
