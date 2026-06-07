using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Releases crashed single-run leases WITHOUT a deadlock. A lease whose
/// heartbeat is stale is only a CANDIDATE — the reaper asks the liveness probe
/// for POSITIVE EVIDENCE the run's container/pod is gone before it DELETEs the
/// row. A stale heartbeat with a still-live container is left alone (no blind
/// time-based release), so a flushed Redis / slow run never triggers a swarm.
/// Re-scopes p0242; supersedes StaleJobDetector's revert-without-cancel.
/// </summary>
public sealed class ActiveRunReaper(
    IActiveRunLease lease,
    IRunLivenessProbe livenessProbe,
    ILogger<ActiveRunReaper> logger)
{
    public async Task<int> RunOnceAsync(TimeSpan staleThreshold, CancellationToken cancellationToken)
    {
        var candidates = await lease.FindStaleAsync(staleThreshold, cancellationToken);
        var released = 0;
        foreach (var candidate in candidates)
        {
            if (await livenessProbe.IsRunPresentAsync(candidate, cancellationToken))
            {
                logger.LogDebug(
                    "Lease {Project}/{Ticket} stale but its run is still present — not releasing",
                    candidate.Project, candidate.TicketId.Value);
                continue;
            }

            await lease.ReleaseAsync(candidate.Project, candidate.TicketId, cancellationToken);
            released++;
            logger.LogWarning(
                "Released crashed lease {Project}/{Ticket} (run={Run}, job={Job}) on positive evidence "
                + "the container is gone — the ticket is reclaimable",
                candidate.Project, candidate.TicketId.Value, candidate.RunId ?? "—", candidate.JobId ?? "—");
        }
        return released;
    }

    public async Task RunAsync(TimeSpan staleThreshold, TimeSpan scanInterval, CancellationToken cancellationToken)
    {
        logger.LogInformation("ActiveRunReaper started (stale>{Stale}, scan {Scan})", staleThreshold, scanInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(staleThreshold, cancellationToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "ActiveRunReaper scan failed"); }

            try { await Task.Delay(scanInterval, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
