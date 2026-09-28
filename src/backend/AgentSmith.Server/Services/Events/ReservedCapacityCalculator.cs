using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0332: RESERVED capacity-time for a finished run — memory request x lifetime in
/// Gi·minutes, summed over the run's pods. Honest label: this is what the scheduler
/// set aside (what a requests-based quota counts), NOT measured consumption.
/// <para>
/// p0404: split out of RunSnapshotMapper — the mapper shapes a run row into the
/// dashboard's contract, this computes one derived quantity from the pod lifetimes.
/// </para>
/// </summary>
public static class ReservedCapacityCalculator
{
    private const double BytesPerGi = 1024d * 1024d * 1024d;

    /// <summary>
    /// Only computed for finished runs; a sandbox that never got a close event ends
    /// with the run (the pods are owner-referenced/disposed at run end). Null when
    /// nothing is computable (pre-p0332 rows) — no fake zeros.
    /// </summary>
    public static double? Compute(Run run)
    {
        if (run.FinishedAt is not { } finished) return null;

        var total = 0d;
        var any = false;
        foreach (var box in run.Sandboxes)
        {
            if (box.SpawnedAt is not { } spawned) continue; // pre-p0332 row
            var request = box.MemoryRequest ?? ResourceLimits.Default.MemoryRequest;
            if (!KubernetesQuantity.TryParseMemoryToBytes(request, out var bytes)) continue;
            total += GiMinutes(spawned, box.DisposedAt ?? finished, bytes);
            any = true;
        }

        // A run recorded with a JobId ran in a spawned orchestrator pod that lived for the
        // whole run; no run is spawned that way any more, but those rows still reserved it.
        if (run.JobId is not null
            && KubernetesQuantity.TryParseMemoryToBytes(ResourceLimits.Default.MemoryRequest, out var orchestratorBytes))
        {
            total += GiMinutes(run.StartedAt, finished, orchestratorBytes);
            any = true;
        }

        return any ? total : null;
    }

    private static double GiMinutes(DateTimeOffset from, DateTimeOffset to, long requestBytes) =>
        Math.Max(0d, (to - from).TotalMinutes) * (requestBytes / BytesPerGi);
}
