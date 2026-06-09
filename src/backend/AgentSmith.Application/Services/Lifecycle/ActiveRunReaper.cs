using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Housekeeping for crashed single-run leases. The DB heartbeat IS the failure
/// detector: ExecutePipelineUseCase renews ActiveRun.HeartbeatAt every 45s for
/// the WHOLE run, independent of step progress, so a heartbeat older than the
/// stale threshold means the owning REPLICA is dead (the pump stopped) — the
/// lease is released, the ticket reclaimable.
///
/// p0258: the positive-evidence liveness PROBE is gone. It asked the orchestrator
/// whether the run's SANDBOX container was still present and kept the lease while
/// it was — but the sandbox is the wrong liveness signal: after the owning replica
/// dies its orphaned sandbox lingers (no consumer, idles out), and the probe then
/// PINNED the ticket behind that zombie for the whole idle-timeout (the "stuck on
/// pending since relational" regression). The only liveness that matters is the
/// run's own DB heartbeat — multi-replica-safe and survives pod-replacement: a
/// live run renews, so its lease never looks stale; a dead replica's lease simply
/// ages out (no owner identity needed).
/// </summary>
public sealed class ActiveRunReaper(
    IActiveRunLease lease,
    IRunCancellationRegistry cancellationRegistry,
    IEventPublisher eventPublisher,
    TimeProvider timeProvider,
    ILogger<ActiveRunReaper> logger)
{
    // p0262: the one freshness threshold for "is this lease a live run?" — relocated
    // here from the deleted StaleJobDetector. EnqueuedReconciler shares it; the poller's
    // in-flight skip treats ANY present lease as in-flight (the reaper removes stale ones).
    public static readonly TimeSpan LeaseFreshFor = TimeSpan.FromMinutes(3);

    public async Task<int> RunOnceAsync(TimeSpan staleThreshold, CancellationToken cancellationToken)
    {
        var candidates = await lease.FindStaleAsync(staleThreshold, cancellationToken);
        var released = 0;
        foreach (var candidate in candidates)
        {
            // p0262: cancel the run BEFORE releasing the lease (moved here from the deleted
            // StaleJobDetector). A stale heartbeat means the owning replica is gone, so this
            // is mostly a formality for THIS replica, but the cross-process
            // RunCancelRequestedEvent marks the run cancelled for any live consumer and the
            // projection — and guarantees no zombie survives next to a fresh re-spawn.
            if (candidate.RunId is { Length: > 0 } runId)
            {
                cancellationRegistry.TryCancel(runId, "stale-lease-reaped");
                await eventPublisher.PublishAsync(
                    new RunCancelRequestedEvent(runId, "stale-lease-reaped", timeProvider.GetUtcNow()),
                    cancellationToken);
            }
            await lease.ReleaseAsync(candidate.Project, candidate.TicketId, cancellationToken);
            released++;
            logger.LogWarning(
                "Reaped crashed lease {Project}/{Ticket} (run={Run}, job={Job}) — DB heartbeat stale, "
                + "owning replica gone: run cancelled + lease released; the ticket is reclaimable",
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
