using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Models;

// Python: DZFile class, _dz_length = 512
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 512)]
public unsafe struct DzHeader
{
    public fixed byte Magic[4];         // header '4s'
    public uint FormatMajor;            // formatMajor 'I'
    public uint FormatMinor;            // formatMinor 'I'
    public uint Reserved0;              // reserved0 'I'
    public fixed byte Device[32];       // device '32s'
    public fixed byte Version[144];     // version '144s'
    public uint ChunkCount;             // chunkCount 'I'
    public fixed byte Md5[16];          // md5 '16s'
    public uint Unknown0;               // unknown0 'I'
    public uint Reserved1;              // reserved1 'I'
    public ushort Reserved4;            // reserved4 'H'
    public fixed byte Unknown1[16];     // unknown1 '16s'
    public fixed byte Unknown2[50];     // unknown2 '50s'
    public fixed byte BuildType[20];    // buildType '20s'
    public fixed byte Unknown3[4];      // unknown3 '4s'
    public fixed byte AndroidVer[10];   // androidVer '10s'
    public fixed byte OldDateCode[10];  // oldDateCode '10s'
    public uint Reserved5;              // reserved5 'I'
    public uint Unknown4;               // unknown4 'I'
    public ulong Unknown5;              // unknown5 'Q'
    public fixed byte Pad[164];         // pad '164s'

    public bool IsValid()
    {
        fixed (byte* p = Magic)
        {
            // \x32\x96\x18\x74
            return p[0] == 0x32 && p[1] == 0x96 && p[2] == 0x18 && p[3] == 0x74;
        }
    }
}
