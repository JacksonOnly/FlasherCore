using System;
using FlasherCore.Firmware.Types; 

namespace FlasherCore.Firmware.Models;

public class OfpQcFileInfo
{
    public int Index;
    public string? Name;
    public string? FileName;
    public ulong Start;
    public ulong Length;
    public ulong RealLength;
    public string? Sha256;
    public string? Md5;
    public OfpQcFileType DumpType;
}