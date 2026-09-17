using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlasherCore.Firmware.Models.Ops;

public class BasicInfo
{
    public string Project { get; set; }
    public string Version { get; set; }
    public string MemoryName { get; set; }
    public string TargetName { get; set; }
    public bool GrowLastPartToFillDisk { get; set; }
    public bool LogEnable { get; set; }
    public int LogPositionIndex { get; set; }
    public int DelayStartTime { get; set; }
    public bool UseGPT { get; set; }
    public bool CheckImage { get; set; }
    public bool CheckHwVersion { get; set; }
    public bool NeedUsbDownload { get; set; }
    public bool BackupPart { get; set; }
    public string BackupPartId { get; set; }
    public int ChipType { get; set; }
    public bool CheckRfVersion { get; set; }
    public bool SkipCheckHWVerByCustFlag { get; set; }
    public string MinToolVersion { get; set; }
    public int ParamVersion { get; set; }
    public int ModelVerifyVersion { get; set; }
    public bool SkipImgSHA256Check { get; set; }
    public string ModelVerifyRandom { get;set;}
    public string ModelVerifyHashToken { get;set;}

}
public class Memory
{
    public int Is16GSupport { get; set; }
    public int Is32GSupport { get; set; }
    public int Is64GSupport { get; set; }
    public int Is128GSupport { get; set; }
    public int Is256GSupport { get; set; }
}
public class File
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public int FileOffsetInSrc { get; set; }
    public int SizeInSectorInSrc { get; set; }
    public long SizeInByteInSrc { get; set; }
}
public class Program
{
    public int SECTOR_SIZE_IN_BYTES { get; set; }
    public int FileSectorOffset { get; set; }
    public bool Sparse { get; set; }
    public string Filename { get; set; }
    public string Label { get; set; }
    public int NumPartitionSectors { get; set; }
    public int PhysicalPartitionNumber { get; set; }
    public int FileOffsetInSrc { get; set; }
    public int SizeInSectorInSrc { get; set; }
    public long SizeInByteInSrc { get; set; }
    public string Sha256 { get; set; }
    public bool PartOfSingleImage { get; set; }
    public bool ReadBackVerify { get; set; }
    public double SizeInKB { get; set; }
    public string StartByteHex { get; set; }
    public string StartSector { get; set; }

}
public class Patch
{
    public int SECTOR_SIZE_IN_BYTES { get; set; }
    public int ByteOffset { get; set; }
    public string Filename { get; set; }
    public int PhysicalPartitionNumber { get; set; }
    public int SizeInBytes { get; set; }
    public string StartSector { get; set; }
    public string Value { get; set; }
    public string What { get; set; }
}
public class MemorySize
{
    public List<Memory> MemorySizes { get; set; }
}
public class Sahara
{
    public List<File> Files;
}
public class UfsProvision
{
    public List<File> Files;
}