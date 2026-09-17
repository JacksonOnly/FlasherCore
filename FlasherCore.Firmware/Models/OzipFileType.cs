using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Firmware.Models;

public enum OzipFileType : int
{
    Payload = 0,
    ZipEntryPlain = 1,
    ZipEntryEncrypted = 2
}
