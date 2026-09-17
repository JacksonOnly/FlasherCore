using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Models;

// Python: _dz_length = 272 (256s + Q + Q)
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 272)]
public unsafe struct KdzEntry
{
    // ('name', '256s')
    public fixed byte Name[256];

    // ('length', 'Q') - unsigned long long
    public ulong Length;

    // ('offset', 'Q')
    public ulong Offset;

    public string GetName()
    {
        fixed (byte* ptr = Name)
        {
            var span = new ReadOnlySpan<byte>(ptr, 256);
            int length = span.IndexOf((byte)0);
            if (length < 0) length = 256;
            return Encoding.UTF8.GetString(span.Slice(0, length));
        }
    }
}