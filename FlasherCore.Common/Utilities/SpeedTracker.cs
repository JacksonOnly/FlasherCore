using System;
using System.Diagnostics;

namespace FlasherCore.Common.Utilities;

public class SpeedTracker
{
    private readonly Stopwatch _sw;
    private readonly TimeSpan _updateInterval;
    private ulong _accumulatedBytes;
    protected Action<ulong>? _onSpeedUpdate;
    public SpeedTracker(Action<ulong>? onSpeedUpdate, int intervalMs = 500)
    {
        _sw = Stopwatch.StartNew();
        _updateInterval = TimeSpan.FromMilliseconds(intervalMs);
        _accumulatedBytes = 0;
        _onSpeedUpdate = onSpeedUpdate;
    }

    public void Reset()
    {
        _accumulatedBytes = 0;
        _sw.Restart();
    }

    public void Update(ulong bytesProcessed)
    {
        if (_onSpeedUpdate == null) return;

        _accumulatedBytes += bytesProcessed;

        if (_sw.Elapsed >= _updateInterval)
        {
            TriggerUpdate(_onSpeedUpdate);
        }
    }
    public void Update(long bytesProcessed)
    {
        if(bytesProcessed > 0)
            Update((ulong)bytesProcessed);
    }
    public void Flush()
    {
        if (_onSpeedUpdate != null && _accumulatedBytes > 0)
        {
            TriggerUpdate(_onSpeedUpdate);
        }
    }

    private void TriggerUpdate(Action<ulong> callback)
    {
        double seconds = _sw.Elapsed.TotalSeconds;

        if (seconds < 0.0001) seconds = 0.0001;

        ulong speed = (ulong)(_accumulatedBytes / seconds);

        callback(speed);

        _accumulatedBytes = 0;
        _sw.Restart();
    }
}