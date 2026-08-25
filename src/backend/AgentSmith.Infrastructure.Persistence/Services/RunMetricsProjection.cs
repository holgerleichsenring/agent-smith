using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0369: the run's incremental metrics fold — deserialize what the row holds, apply one
/// event, write it back. The stored JSON IS the fold state (including the per-path
/// last-content-hash that makes redundancy content-aware), which is why it round-trips
/// through the row rather than being recomputed.
/// <para>
/// 2026-08-25-61f1: its own service because two projections fold onto it — the per-call
/// one and the sandbox one — and a fold that lives in one of them is a fold the other has
/// to copy.
/// </para>
/// </summary>
public sealed class RunMetricsProjection
{
    public void Fold(Run run, AgentSmith.Contracts.Events.RunEvent e)
    {
        var metrics = RunStoryJson.TryDeserialize<RunMetrics>(run.RunMetricsJson) ?? new RunMetrics();
        metrics.Apply(e);
        run.RunMetricsJson = RunStoryJson.Serialize(metrics);
    }
}
