

//var zipUrl = "https://ultimateota.d.miui.com/OS2.0.210.0.VLBCNXM/zeus-ota_full-OS2.0.210.0.VLBCNXM-user-15.0-8b1325b4b3.zip?t=1766816522&s=e9ebad3ac40464c134de7fe73a5bdf67";
//using var stream = BufferedHttpStream.CreateAsync(zipUrl,
//    blockSize: 4 * 1024 * 1024,
//    downloadConcurrency: 4
//).GetAwaiter().GetResult();
//using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
//foreach (var entry in archive.Entries)
//{
//    Console.WriteLine(entry.Name);
//}
//var payload = archive.Entries.FirstOrDefault(it => it.Name == "payload.bin");
//using var payloadStm = payload.Open();
//using var output = File.OpenWrite("D:\\ROM\\test_payload.bin");
//Span<byte> buffer = new byte[1024*1024];
//ulong current = 0;
//ulong bytesToRead = (ulong)payloadStm.Length;
//while(bytesToRead > 0)
//{
//    var size = Math.Min((uint)buffer.Length, bytesToRead);
//    payloadStm.ReadExactly(buffer);
//    output.Write(buffer);
//    current +=(uint) size;
//    bytesToRead-= size;
//    Unpacker_ProgressEvent(IntPtr.Zero, current, bytesToRead);
//}
//return;

/*
using FlasherCore.Common.Types;
using FlasherCore.Common.Utilities;
using FlasherCore.Firmware;
using FlasherCore.Firmware.Types;
using System.Diagnostics;

unsafe
{
    var ofpQc = @"D:\BaiduNetdiskDownload\PEHM00domestic_11_A.17_2022031122400000.ofp";
    var ofpMtk = @"D:\BaiduNetdiskDownload\PFGM00domestic_11_C.19_2023101911460121.ofp";
    var opsQc = @"D:\BaiduNetdiskDownload\enchilada_22_K.52_210716.ops";
    var kdz = @"D:\ROM\V500EM40b_00_OPEN_EU_OP_0517.kdz";
    var dz = @"D:\ROM\test.dz";
    var payloadZip = @"D:\ROM\miui_ALIOTH_OS1.0.10.0.TKHCNXM_ddf5cf5ea0_13.0.zip";
    var payload = @"D:\ROM\miui_ALIOTH_OS1.0.10.0.TKHCNXM_ddf5cf5ea0_13.0\payload.bin";
    var updataApp = @"D:\ROM\UPDATE.APP";
    var ozip = @"D:\Downloads\PDEM10_11_OTA_0500_all_i5OwKR2W0S7D.ozip";
    using var stm = File.OpenRead(payloadZip);
    FWSession session = new FWSession()
    {
        InputType = InOutType.Stream,
        InputStream =stm.AsInteropStream(true),
    };
    session.Speed = Unpacker_SpeedEvent;
    session.Progress = Unpacker_ProgressEvent;
    session.InitializeTool();
    var count = session.Tool.GetEntryCount();

    Span<UPFirmwareEntry> entries = new UPFirmwareEntry[count];
    fixed (UPFirmwareEntry* ptr = entries)
    {
        session.Tool.GetEntries(ptr, count);
    }
    InOutOptions inOutOptions;
    session.Tool.OpenEntryStream(0, &inOutOptions);
    using var testStm  = inOutOptions.Stream.AsStream();
    using var fs = File.OpenWrite("boot");
    testStm.CopyTo(fs);
    //for (ushort i = 0; i < count; i++)
    //{
    //    option.Id = i;
    //    option.SetFileName(Path.Combine("D:\\ROM\\TEST_KDZ_DZ", entries[i].GetFileName()));
    //    if (session.Tool.Run(&option) != FlasherCore.Common.Types.FlasherCoreResult.Success)
    //    {
    //        Debug.Assert(true);
    //    }
    //}
}

*/


using FlasherCore.Sdk;
using FlasherCore.Sdk.Types;
using FlasherCore.Sdk.Types.Firmware;
using FlasherCore.Sdk.Extensions;
using System.IO.Pipes;
using System.Diagnostics;

var ofpQc = @"D:\BaiduNetdiskDownload\PEHM00domestic_11_A.17_2022031122400000.ofp";
var ofpMtk = @"D:\BaiduNetdiskDownload\PFGM00domestic_11_C.19_2023101911460121.ofp";
var opsQc = @"D:\BaiduNetdiskDownload\enchilada_22_K.52_210716.ops";
var pac = @"D:\ROM\iPlay40(T1020S)_酷比魔方OS_20220424-固件及教程\线刷固件\T1020S-ALLDOCUBEOS-20220424.pac";
var dz = @"D:\ROM\V50040b_0_user-signed-ARB0_OPEN_EU_OP_0517.dz";
var kdz = @"D:\ROM\V500EM40b_00_OPEN_EU_OP_0517.kdz";
var payload = @"D:\ROM\miui_ALIOTH_OS1.0.10.0.TKHCNXM_ddf5cf5ea0_13.0.zip";
var dllPath = @"D:\Code\Project\FlasherCore\Lib\FlasherCore\FlasherCore.Firmware\bin\Release\net10.0\publish\win-x86\FlasherCore.Firmware.dll";
using Firmware unpacker = new Firmware(dllPath);
unpacker.ProgressEvent += Unpacker_ProgressEvent;
unpacker.SpeedEvent += Unpacker_SpeedEvent;

unpacker.SetInput(new InOutOptions()
{
    Type = InOutType.File,
    FileName = @"D:\ROM\miui_ALIOTH_OS1.0.10.0.TKHCNXM_ddf5cf5ea0_13.0.zip"

}); 

var entries = unpacker.GetEntries();
Console.WriteLine(entries.Length);
Console.WriteLine(entries);

for(var i = 0; i< entries.Length; i++ )
{
    Console.WriteLine(entries[i].Name);
}

while(true)
{
    Console.WriteLine("Sleeping ... ");
    Thread.Sleep(2000);
}
var bootItem = entries.First(it => it.Name.Equals("system", StringComparison.OrdinalIgnoreCase));
unpacker.Run(bootItem.Index, new InOutOptions()
{
    FileName = "system",
    Type = InOutType.File
});



return;

void Unpacker_SpeedEvent(IntPtr userdata, ulong perSecondByteCount)
{
    Console.Title = perSecondByteCount.ToSpeedString();
}
void Unpacker_ProgressEvent(IntPtr userdata, ulong current, ulong total)
{
    Console.WriteLine("{0} -> {1} | {2:00}%", current, total, (double)current / (double)total * 100);
}
static class FormatExtensions
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

    public static string ToSizeString(this ulong bytes, int decimalPlaces = 2)
    {
        if (bytes == 0) return "0 B";

        const int scale = 1024;


        int order = (int)(Math.Log(bytes) / Math.Log(scale));

        if (order >= SizeSuffixes.Length) order = SizeSuffixes.Length - 1;

        double adjustedSize = bytes / Math.Pow(scale, order);

        return $"{adjustedSize.ToString($"N{decimalPlaces}")} {SizeSuffixes[order]}";
    }
    public static string ToSpeedString(this ulong bytesPerSecond)
    {
        return $"{bytesPerSecond.ToSizeString()}/s";
    }

    public static string ToSizeString(this long bytes, int decimalPlaces = 2)
    {
        if (bytes < 0) return "-" + ToSizeString((ulong)-bytes, decimalPlaces);
        return ToSizeString((ulong)bytes, decimalPlaces);
    }
}
