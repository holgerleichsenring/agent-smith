namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0336: a run's COMPLETE, pre-computed pod footprint — every sandbox (after
/// scoping) plus the orchestrator, each with its resolved limit. Computed once
/// at admission from the remote context inventory (no estimate, no mid-run
/// growth) and reserved atomically against the capacity budget before the run
/// starts. The byte/nano totals are the summable figures the ledger gates on;
/// the string totals + pods + dropped list are the operator-facing calculation.
/// </summary>
public sealed record RunFootprintBreakdown(
    IReadOnlyList<RunFootprintPod> Pods,
    string TotalCpuLimit,
    string TotalMemLimit,
    long TotalCpuNanos,
    long TotalMemBytes,
    IReadOnlyList<DroppedContext> Dropped,
    string Reason)
{
    public static RunFootprintBreakdown Empty { get; } =
        new([], "0", "0", 0, 0, [], "no footprint computed");
}
