using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FlasherCore.Common.Utilities
{
    public static class StopWatchUtils
    {
        public static ulong GetSpeedBytesPerSecond(this Stopwatch sw, ulong writtenByteCount)
        {
            double seconds = sw.Elapsed.TotalSeconds;

            if (seconds < 0.000001)
            {
                return 0;
            }

            var speed = (ulong)(writtenByteCount / seconds);

            sw.Restart();

            return speed;
        }
    }
}
