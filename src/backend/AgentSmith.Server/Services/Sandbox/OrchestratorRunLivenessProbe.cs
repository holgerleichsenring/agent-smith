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
            logger.LogDebug(
                "Lease {Project}/{Ticket} has no orchestrator handle yet — treating as present (no release)",
                lease.Project, lease.TicketId.Value);
            return true;
        }

        return await jobSpawner.IsAliveAsync(lease.JobId, cancellationToken);
    }
}
