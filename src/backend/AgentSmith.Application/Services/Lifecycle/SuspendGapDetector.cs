namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// p0383: a monotonic gap far beyond the scan interval means the process (or its host) was
/// suspended — on wake every DB heartbeat is stale by construction, a clock artifact, not a
/// dead replica — so stale verdicts are suppressed for one grace window while the heartbeat
/// pumps catch up. Monotonic time (GetTimestamp), never GetUtcNow: wall-clock jumps are
/// exactly what cannot be trusted here.
/// <para>
/// 2026-10-02-5f89e: extracted from ActiveRunReaper so RunLivenessReaper judges staleness
/// under the same guard. Stateful by design — one instance per scan loop, observed once per
/// iteration; it reads the clock exactly as the inlined original did.
/// </para>
/// </summary>
public sealed class SuspendGapDetector(TimeProvider timeProvider, TimeSpan scanInterval, TimeSpan grace)
{
    private long _previousIteration = timeProvider.GetTimestamp();
    private long? _wake;

    /// <summary>Called once per loop iteration. Returns the gap when this iteration found
    /// one, so the caller can say so; null otherwise.</summary>
    public TimeSpan? Observe()
    {
        var gap = timeProvider.GetElapsedTime(_previousIteration);
        var found = gap > scanInterval + grace / 2;
        if (found) _wake = timeProvider.GetTimestamp();
        _previousIteration = timeProvider.GetTimestamp();
        return found ? gap : null;
    }

    /// <summary>True while the last detected gap is younger than the grace window.</summary>
    public bool SuppressesVerdicts()
    {
        if (_wake is { } wake && timeProvider.GetElapsedTime(wake) < grace) return true;
        _wake = null;
        return false;
    }
}
