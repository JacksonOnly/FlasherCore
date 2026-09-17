using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FlasherCore.Firmware.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct OfpMtkHeader 
{
    public fixed byte PrjName[48];
    public ulong UnknowVal;
    public fixed byte Reserved[4];
    public fixed byte Cpu[7];
    public fixed byte FlashType[5];
    public ushort Hdr2Entries;
    public fixed byte PrjInfo[32];
    public ushort Crc;
}
