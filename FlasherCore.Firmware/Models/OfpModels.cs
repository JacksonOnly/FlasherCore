using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace FlasherCore.Firmware.Models.Ofp;

public class BasicInfo
{
    public string Brand { get; set; }
    public string Project { get; set; }
    public string Version { get; set; }
    public string MemoryName { get; set; }
    public int TotalSectors { get; set; }
    public int GrowLastPartToFillDisk { get; set; }
    public int LogEnable { get; set; }
    public int LogPosition { get; set; }
    public int DelayStartTime { get; set; }
    public int UseGPT { get; set; }
    public int CheckImage { get; set; }
    public int NeedUsbDownload { get; set; }
    public int BackupPart { get; set; }
    public string BackupPartId { get; set; }
    public int ChipType { get; set; }
    public int IsEnterpriseVer { get; set; }
    public int IsSecrecyVer { get; set; }
    public int IsRootVer { get; set; }
    public int IsLockVer { get; set; }
    public int IsCloudServerVer { get; set; }
    public int IsBindingMetadataUserdataVer { get; set; }
    public int IsDownloadConfigVer { get; set; }
    public int IsDownloadConfigSignedVer { get; set; }
    public int IsVerifyCdtVer { get; set; }
    public int IsOcdtVer { get; set; }
    public int IsClearRootConfigVer { get; set; }
    public int SupportFillChunk { get; set; }
    public int SupportFwConfig { get; set; }
    public int ProversionUseFfuFirehose { get; set; }
}

public class File
{
    public int Id { get; set; }
    public string Path { get; set; }
    public int FileOffsetInSrc { get; set; }
    public int SizeInSectorInSrc { get; set; }
    public int SizeInByteInSrc { get; set; }
    public string Md5 { get; set; }
}

public class Sahara
{
    public List<File> File { get; set; }
}

public class Nv
{
    public int Id { get; set; }
    public string Text { get; set; }
    public string Super0 { get; set; }
    public string Super1 { get; set; }
    public string Super2 { get; set; }
}

public class NVList
{
    public List<Nv> Nv { get; set; }
}

public class Program
{
    public int SECTOR_SIZE_IN_BYTES { get; set; }
    public int FileSectorOffset { get; set; }
    public int SkipDownload { get; set; }
    public int NotOverwrite { get; set; }
    public int Sparse { get; set; }
    public string Filepath { get; set; }
    public string Filename { get; set; }
    public string Label { get; set; }
    public int NumPartitionSectors { get; set; }
    public int PhysicalPartitionNumber { get; set; }
    public int FileOffsetInSrc { get; set; }
    public int SizeInSectorInSrc { get; set; }
    public long SizeInByteInSrc { get; set; }
    public string Md5 { get; set; }
    public string Sha256 { get; set; }
    public string StartSector { get; set; }
    public string Project { get; set; }
    public string Module { get; set; }
    public string Emmcsize { get; set; }
    public string Customize { get; set; }
    public string FilenmarketnameXml { get; set; }
    public string SetCloudServerData { get; set; }
    public string Userdataname { get; set; }
}

public class ProgramList
{
    public List<Program> Program { get; set; }
}

public class Super
{
    public List<Program> Program { get; set; }
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
}

public class PatchList
{
    public List<Patch> Patch { get; set; }
}

public class ProjectConfig
{
    public List<Program> Program { get; set; }
}

public class Config
{
    public string Filename { get; set; }
    public int SizeInByteInSrc { get; set; }
    public int SizeInSectorInSrc { get; set; }
    public string Md5 { get; set; }
    public string Sha256 { get; set; }
}

public class ConfigList
{
    public List<Config> Config { get; set; }
}

public class Provision
{
    public List<Config> Config { get; set; }
}

public class ChainedTableOfDigests
{
    public List<Config> Config { get; set; }
}

public class DigestsToSign
{
    public List<Config> Config { get; set; }
}

