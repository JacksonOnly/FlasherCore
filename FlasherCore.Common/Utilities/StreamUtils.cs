using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Common.Utilities
{
    public static class StreamUtils
    {
        private static ReadOnlySpan<byte> ZipMagic => new byte[] { 0x50, 0x4B, 0x03, 0x04 };

        public static bool IsZipStream(this Stream stream)
        {
            if(stream.CanSeek)
            {
                var pos = stream.Position;
                stream.Seek(0, SeekOrigin.Begin);
                try
                {
                    Span<byte> magic = stackalloc byte[4];
                    stream.ReadExactly(magic);
                    return magic.SequenceEqual(ZipMagic);
                }
                finally
                {
                    stream.Seek(pos, SeekOrigin.Begin);
                }
                
            }
            return false;
        }
    }
}
