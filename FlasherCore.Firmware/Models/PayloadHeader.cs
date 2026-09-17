using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlasherCore.Unpacker;

public class PayloadHeader
{
    public uint Magic;
    public ulong FileFormatVersion;
    public ulong ManifestSize;
    public uint MetadataSignatureSize;
    public PayloadHeader()
    {

    }
    public PayloadHeader(byte[] data)
    {
        StructHelper sh = new StructHelper(data);
        Magic =  sh.Dword();
        FileFormatVersion = sh.Qword(true);
        ManifestSize = sh.Qword(true);
        MetadataSignatureSize = sh.Dword(true);

    }
}
