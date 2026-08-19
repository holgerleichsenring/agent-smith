using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
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
///
/// p0383: two false-positive guards on that verdict. (1) A stale heartbeat for a
/// run that is registered and un-cancelled in THIS process is a lagging pump, not
/// a dead replica — the heartbeat is refreshed, never reaped. The per-process
/// registry keeps multi-replica semantics: each replica protects only its own
/// runs. (2) A monotonic gap across scan iterations means the process was
/// suspended (host sleep): on wake every heartbeat is stale by construction, so
/// stale verdicts are suppressed for one LeaseFreshFor window while the pumps
/// catch up.
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
            // p0383: liveness guard — a run alive in this process is never reaped.
            if (candidate.RunId is { Length: > 0 } runId && cancellationRegistry.IsLocallyActive(runId))
            {
                await RefreshLaggingHeartbeatAsync(candidate, cancellationToken);
                continue;
            }
            await ReapAsync(candidate, cancellationToken);
            released++;
        }
        return released;
    }

    public async Task RunAsync(TimeSpan staleThreshold, TimeSpan scanInterval, CancellationToken cancellationToken)
    {
        logger.LogInformation("ActiveRunReaper started (stale>{Stale}, scan {Scan})", staleThreshold, scanInterval);
        var previousIteration = timeProvider.GetTimestamp();
        long? wakeTimestamp = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            wakeTimestamp = DetectSuspendGap(previousIteration, scanInterval) ?? wakeTimestamp;
            previousIteration = timeProvider.GetTimestamp();
            if (wakeTimestamp is not { } wake || timeProvider.GetElapsedTime(wake) >= LeaseFreshFor)
            {
                wakeTimestamp = null;
                try { await RunOnceAsync(staleThreshold, cancellationToken); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { logger.LogError(ex, "ActiveRunReaper scan failed"); }
            }
            try { await Task.Delay(scanInterval, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    // p0383: the stale verdict is provably false while the run is registered and
    // un-cancelled here — refresh the heartbeat instead. This firing at all means
    // the heartbeat pump is lagging behind the stale threshold: warn, name the run.
    private async Task RefreshLaggingHeartbeatAsync(StaleLease candidate, CancellationToken cancellationToken)
    {
        await lease.RenewHeartbeatAsync(candidate.Project, candidate.TicketId, cancellationToken);
        logger.LogWarning(
            "Spared stale lease {Project}/{Ticket}: run {Run} is alive in this process — "
            + "heartbeat refreshed instead of reaped (the heartbeat pump is behind)",
            candidate.Project, candidate.TicketId.Value, candidate.RunId);
    }

    private async Task ReapAsync(StaleLease candidate, CancellationToken cancellationToken)
    {
        // p0262: cancel the run BEFORE releasing the lease (moved here from the deleted
        // StaleJobDetector). A stale heartbeat means the owning replica is gone, so this
        // is mostly a formality for THIS replica, but the cross-process
        // RunCancelRequestedEvent marks the run cancelled for any live consumer and the
        // projection. p0459: the release names the lease's OWN run, never just the ticket.
        if (candidate.RunId is { Length: > 0 } runId)
        {
            cancellationRegistry.TryCancel(runId, "stale-lease-reaped");
            await eventPublisher.PublishAsync(
                new RunCancelRequestedEvent(runId, "stale-lease-reaped", timeProvider.GetUtcNow()),
                cancellationToken);
        }
        await lease.ReleaseAsync(candidate.Project, candidate.TicketId, candidate.RunId, cancellationToken);
        logger.LogWarning(
            "Reaped crashed lease {Project}/{Ticket} (run={Run}, job={Job}) — DB heartbeat stale, "
            + "owning replica gone: run cancelled + lease released; the ticket is reclaimable",
            candidate.Project, candidate.TicketId.Value, candidate.RunId ?? "—", candidate.JobId ?? "—");
    }

    // p0383: a monotonic gap far beyond the scan interval means the process (or its
    // host) was suspended — on wake every DB heartbeat is stale by construction, a
    // clock artifact, not a dead replica. Monotonic time (GetTimestamp), never
    // GetUtcNow: wall-clock jumps are exactly what cannot be trusted here.
    private long? DetectSuspendGap(long previousIteration, TimeSpan scanInterval)
    {
        var gap = timeProvider.GetElapsedTime(previousIteration);
        if (gap <= scanInterval + LeaseFreshFor / 2) return null;
        logger.LogWarning(
            "ActiveRunReaper detected a suspend gap of {Gap} (scan interval {Scan}) — "
            + "suppressing stale verdicts for {Grace} while heartbeat pumps catch up",
            gap, scanInterval, LeaseFreshFor);
        return timeProvider.GetTimestamp();
    }
}
