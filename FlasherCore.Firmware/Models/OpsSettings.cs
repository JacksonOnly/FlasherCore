using FlasherCore.Firmware.Models.Ops;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlasherCore.Firmware.Models;

public class OpsSettings
{
    public BasicInfo BasicInfo { get; set; }
    public MemorySize MemorySize { get; set; }
    public Sahara Sahara { get; set; }
    public UfsProvision UfsProvision { get; set; }
    public List<Program>[] Programs { get; set; }
    public List<Patch>[] Patches { get; set; }
}
