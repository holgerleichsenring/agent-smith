using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// The reaper's POSITIVE-EVIDENCE source, backed by the orchestrator. Asks
/// IJobSpawner (K8s/Docker) whether the lease's container/pod is still present —
/// authoritative even when Redis was flushed (it queries the runtime, not Redis),
/// so it cannot trigger the empty-Redis mass-release. A lease with no orchestrator
/// handle yet (claimed but not spawned) is treated as PRESENT — a lease is never
/// released without proof.
/// </summary>
public sealed class OrchestratorRunLivenessProbe(
    IJobSpawner jobSpawner,
    ILogger<OrchestratorRunLivenessProbe> logger) : IRunLivenessProbe
{
    public async Task<bool> IsRunPresentAsync(StaleLease lease, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(lease.JobId))
        {
            // p0242: a lease with no orchestrator job is an IN-PROCESS run (or a
            // claim that never started one). An in-process run keeps its lease
            // alive by renewing the heartbeat (ExecutePipelineUseCase). The reaper
            // only probes leases whose heartbeat is ALREADY stale — so a null-job
            // candidate is a run that stopped renewing: crashed, or a claim that
            // never executed. The stale heartbeat IS the proof of death — release
            // it. (Previously this returned "present", which pinned a finished
            // in-process run's lease forever and blocked the ticket from ever
            // running again — the leak fixed in p0242.)
            logger.LogDebug(
                "Lease {Project}/{Ticket} has a stale heartbeat and no orchestrator handle — treating as gone",
                lease.Project, lease.TicketId.Value);
            return false;
        }

        return await jobSpawner.IsAliveAsync(lease.JobId, cancellationToken);
    }
}
