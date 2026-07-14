namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0336: the app-owned capacity ledger — the PREDICTABLE admission gate. A run's
/// complete footprint is recorded (for visibility) and reserved atomically
/// against a fixed budget before it starts; a run only starts when its full
/// footprint fits the remaining budget, so a started run can never fail for
/// capacity mid-run. The invariant is sum(reserved footprints of running runs)
/// &lt;= budget. Released when the run reaches a terminal status (or is deleted).
/// The k8s ResourceQuota stays the hard backstop; this ledger is the lag-free
/// gate the quota's eventually-consistent Status.Used cannot be.
/// </summary>
public interface ICapacityBudget
{
    /// <summary>Persist the run's computed footprint (queued OR running) for the
    /// dashboard calculation panel — no budget is held until <see cref="TryReserveAsync"/>.</summary>
    Task RecordAsync(string runId, RunFootprintBreakdown footprint, CancellationToken ct);

    /// <summary>Atomically reserve the run's recorded footprint if it fits the
    /// remaining budget. True = reserved (safe to start); false = does not fit
    /// (keep waiting). Fail-open when no budget is configured.</summary>
    Task<bool> TryReserveAsync(string runId, CancellationToken ct);

    /// <summary>Free the run's reservation — called on terminal status or delete.</summary>
    Task ReleaseAsync(string runId, CancellationToken ct);

    /// <summary>The run's recorded footprint + whether it currently holds a
    /// reservation, for the run snapshot. Null when nothing was recorded.</summary>
    Task<RunCapacitySnapshot?> GetAsync(string runId, CancellationToken ct);

    /// <summary>Recorded footprints for many runs in one read — the dashboard
    /// overview joins these onto its run list. Missing runs are simply absent.</summary>
    Task<IReadOnlyDictionary<string, RunCapacitySnapshot>> GetManyAsync(
        IReadOnlyCollection<string> runIds, CancellationToken ct);
}

/// <summary>p0336: a run's recorded footprint plus its live reservation state.</summary>
public sealed record RunCapacitySnapshot(RunFootprintBreakdown Footprint, bool Reserved);
