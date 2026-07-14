using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0336: the run's capacity calculation as the dashboard reads it — the pods
/// (each with its resolved limit), the totals, the dropped repos/contexts (+why),
/// the human reason, and whether the run currently holds a budget reservation.
/// Populated on the run-detail snapshot from the capacity ledger.
/// </summary>
public sealed record RunFootprintView(
    IReadOnlyList<RunFootprintPod> Pods,
    string TotalCpuLimit,
    string TotalMemLimit,
    IReadOnlyList<DroppedContext> Dropped,
    string Reason,
    bool Reserved)
{
    public static RunFootprintView? From(RunCapacitySnapshot? capacity) =>
        capacity is null
            ? null
            : new RunFootprintView(
                capacity.Footprint.Pods, capacity.Footprint.TotalCpuLimit, capacity.Footprint.TotalMemLimit,
                capacity.Footprint.Dropped, capacity.Footprint.Reason, capacity.Reserved);
}
