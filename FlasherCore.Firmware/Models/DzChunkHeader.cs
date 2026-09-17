using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FlasherCore.Firmware.Models
{

    // Python: DZChunk class, _dz_length = 512
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 512)]
    public unsafe struct DzChunkHeader
    {
        public fixed byte Magic[4];         // header '4s'
        public fixed byte SliceName[32];    // sliceName '32s'
        public fixed byte ChunkName[64];    // chunkName '64s'
        public uint TargetSize;             // targetSize 'I' (Uncompressed size)
        public uint DataSize;               // dataSize 'I' (Compressed size)
        public fixed byte Md5[16];          // md5 '16s'
        public uint TargetAddr;             // targetAddr 'I'
        public uint TrimCount;              // trimCount 'I'
        public uint Dev;                    // dev 'I'
        public uint Crc32;                  // crc32 'I'
        public fixed byte Pad[372];         // pad '372s'

        public string GetChunkName()
        {
            fixed (byte* ptr = ChunkName)
            {
                var span = new ReadOnlySpan<byte>(ptr, 64);
                int length = span.IndexOf((byte)0);
                if (length < 0) length = 64;
                return Encoding.UTF8.GetString(span.Slice(0, length));
            }
        }
    }
}
