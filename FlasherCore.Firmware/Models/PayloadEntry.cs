using ChromeosUpdateEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlasherCore.Firmware.Models;

public class PayloadEntry
{
    public string Name;
    public ulong Size;
    public PartitionUpdate Partition; // Protobuf 对象
}
