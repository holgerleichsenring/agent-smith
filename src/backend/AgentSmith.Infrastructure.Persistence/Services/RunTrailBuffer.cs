using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Per-run accumulator for the raw event trail. Add returns the batch to flush
/// (and clears) when the threshold is hit or the run finishes, else null —
/// turning one-insert-per-event into batched inserts. Each event is stamped with
/// a monotonic per-run sequence number.
///
/// p0376: a size-only threshold left the trail dark until 25 events piled up (or
/// the run finished), so a run that emits fewer events, or pauses mid-batch, only
/// surfaced its trail in sudden bursts. <see cref="DrainIfOlderThan"/> lets a
/// background flusher drain a partial buffer once its oldest pending event has
/// waited too long, so the UI trail stays live without giving up batched inserts
/// on the hot path.
///
/// <para>2026-08-24-ca23: the sequence STARTS where the store already ended for this run.
/// The counter is per-instance, so a buffer created for a run that already has history — a
/// relaunch after a pause, or a run alive across a server restart — used to restart at zero
/// and mint sequences the previous instance had already written, which is what let replayed
/// rows collide by value instead of being recognised.</para>
/// </summary>
public sealed class RunTrailBuffer(long startSeq = 0)
{
    private readonly object _gate = new();
    private readonly List<(long Seq, RunEvent Event)> _pending = new();
    private long _seq = startSeq;
    private DateTimeOffset _firstPendingAt;

    public IReadOnlyList<(long Seq, RunEvent Event)>? Add(RunEvent runEvent, int flushThreshold, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_pending.Count == 0) _firstPendingAt = now;
            _pending.Add((_seq++, runEvent));
            if (_pending.Count < flushThreshold && runEvent.Type != EventType.RunFinished)
                return null;
            return Drain();
        }
    }

    /// <summary>
    /// p0376: drain a partial buffer once its oldest pending event is older than
    /// <paramref name="maxAge"/>. Returns null when there is nothing pending or the
    /// oldest pending event is still fresh, so the background flusher only writes
    /// when there is genuinely stale trail to surface.
    /// </summary>
    public IReadOnlyList<(long Seq, RunEvent Event)>? DrainIfOlderThan(TimeSpan maxAge, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_pending.Count == 0 || now - _firstPendingAt < maxAge) return null;
            return Drain();
        }
    }

    private IReadOnlyList<(long Seq, RunEvent Event)> Drain()
    {
        var batch = _pending.ToList();
        _pending.Clear();
        return batch;
    }
}
