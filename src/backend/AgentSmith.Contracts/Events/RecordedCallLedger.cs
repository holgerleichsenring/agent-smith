namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-17-7c9bc: the calls this run recorded, in this process, as the event publisher recorded them —
/// the same per-call figures the run row sums (2026-11-17-166ab). One per run, opened as an ambient frame
/// beside the run id, so every chat call of the run adds to it without a constructor dependency. Readers
/// that report the run's spend while it runs (a design turn's proposal, result.md) read it instead of the
/// cost tracker, which sees only the responses its call sites hand it and leaves worker calls out.
/// </summary>
public sealed class RecordedCallLedger
{
    private readonly object _gate = new();
    private readonly List<RecordedCall> _calls = [];
    private RecordedCallCarry _carried = RecordedCallCarry.None;

    public void Add(RecordedCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        lock (_gate) _calls.Add(call);
    }

    /// <summary>A resumed run's earlier segments, carried once.</summary>
    public void Carry(RecordedCallCarry prior)
    {
        ArgumentNullException.ThrowIfNull(prior);
        lock (_gate) _carried = prior;
    }

    public IReadOnlyList<RecordedCall> Calls
    {
        get { lock (_gate) return [.. _calls]; }
    }

    public RecordedCallCarry Carried
    {
        get { lock (_gate) return _carried; }
    }

    /// <summary>Every recorded call's cost plus what earlier segments carried.</summary>
    public decimal CostUsd
    {
        get { lock (_gate) return _calls.Sum(c => c.CostUsd) + _carried.CostUsd; }
    }
}
