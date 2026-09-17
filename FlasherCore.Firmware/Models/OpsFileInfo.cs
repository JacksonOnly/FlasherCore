using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlasherCore.Firmware.Models;

public class OpsFileInfo
{
    public int Index;
    public string? Name;
    public string? FileName;
    public ulong Start;
    public ulong Length;
    public ulong RealLength;
    public string? Sha256;
    public string? Md5;
    public OpsFileType DumpType;
}
