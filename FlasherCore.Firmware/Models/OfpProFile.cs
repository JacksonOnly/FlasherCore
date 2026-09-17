using FlasherCore.Firmware.Models.Ofp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace FlasherCore.Firmware.Models;
public class OfpProFile
{
    public BasicInfo BasicInfo { get; set; }
    public Sahara Sahara { get; set; }
    public NVList NVList { get; set; }
    public object Firmware { get; set; }
    public ProgramList ProgramList { get; set; }
    public Super Super { get; set; }
    public PatchList PatchList { get; set; }
    public ProjectConfig ProjectConfig { get; set; }
    public ConfigList Config { get; set; }
    public Provision Provision { get; set; }
    public ChainedTableOfDigests ChainedTableOfDigests { get; set; }
    public DigestsToSign DigestsToSign { get; set; }
}

