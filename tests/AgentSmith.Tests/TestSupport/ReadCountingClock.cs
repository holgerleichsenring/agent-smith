namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// A monotonic clock a test moves by hand, counting its reads — a scan loop reads it at least
/// twice per iteration, so a delta of 2N reads proves N iterations ran. Shared by the
/// suspend-gap tests of the liveness reaper (2026-10-02-5f89e) and the queued-run sweeper
/// (2026-10-02-5ab2b).
/// </summary>
internal sealed class ReadCountingClock : TimeProvider
{
    private long _timestamp;
    private long _reads;

    public long Reads => Volatile.Read(ref _reads);

    public override long GetTimestamp()
    {
        Interlocked.Increment(ref _reads);
        return Volatile.Read(ref _timestamp);
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan by) => Interlocked.Add(ref _timestamp, by.Ticks);
}
