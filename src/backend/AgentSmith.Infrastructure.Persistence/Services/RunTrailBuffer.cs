using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Per-run accumulator for the raw event trail. Add returns the batch to flush
/// (and clears) when the threshold is hit or the run finishes, else null —
/// turning one-insert-per-event into batched inserts. Each event is stamped with
/// a monotonic per-run sequence number.
/// </summary>
public sealed class RunTrailBuffer
{
    private readonly object _gate = new();
    private readonly List<(long Seq, RunEvent Event)> _pending = new();
    private long _seq;

    public IReadOnlyList<(long Seq, RunEvent Event)>? Add(RunEvent runEvent, int flushThreshold)
    {
        lock (_gate)
        {
            _pending.Add((_seq++, runEvent));
            if (_pending.Count < flushThreshold && runEvent.Type != EventType.RunFinished)
                return null;
            var batch = _pending.ToList();
            _pending.Clear();
            return batch;
        }
    }
}
