using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0330: the durable cancel guarantee. The cancel endpoint persists
/// CancelRequested + a kill deadline and lets the cooperative token race the
/// grace window; this enforcer scans the DB for flagged, non-terminal runs whose
/// deadline elapsed and force-kills them — TerminateAsync on the spawned
/// orchestrator's Job/container, then finalize 'cancelled' via the event path
/// (single-writer projector), release the lease, terminalize the ticket. All
/// state lives in the run row, so a server restart inside the grace window still
/// guarantees the kill. Runs under the housekeeping leader.
/// </summary>
public sealed class CancelEnforcer(
    IServiceProvider services,
    IEventPublisher events,
    IActiveRunLease lease,
    CancelledTicketFinalizer ticketFinalizer,
    TimeProvider timeProvider,
    ILogger<CancelEnforcer> logger)
{
    // p0348: the wall-time ceiling for a RUNNING run. PipelineRunWatchdog enforces this
    // only over the in-memory registry (in-process runs); a spawned orchestrator run or
    // one that outlived a restart is never registered, so this DB-backed scan closes
    // that gap for every run — including the spawned #19106 migration.
    // p0353: read LIVE per scan (was a boot-frozen IOptions) so a Config Studio edit to
    // orchestrator.max_run_wall_time_seconds applies without a restart.
    private TimeSpan CurrentMaxWallTime() => TimeSpan.FromSeconds(
        services.GetRequiredService<IConfigurationLoader>()
            .LoadConfig(services.GetRequiredService<ServerContext>().ConfigPath)
            .Orchestrator.MaxRunWallTimeSeconds);

    /// <summary>Grace between the persisted cancel and the force-kill — the
    /// window in which a cooperative (in-process) cancel may land first. p0348:
    /// shared with the projector (RunEventApplier), which stamps this same grace
    /// onto the deadline for a watchdog/wall-time cancel so it too gets enforced.</summary>
    public static readonly TimeSpan KillGrace = CancelPolicy.KillGrace;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(15);

    public async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("CancelEnforcer started (grace {Grace}, scan {Scan})", KillGrace, ScanInterval);
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "CancelEnforcer scan failed"); }

            try { await Task.Delay(ScanInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // Public so tests (and the harness) drive single scans deterministically.
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        // p0348: first flag any RUNNING run past the wall-time ceiling — it enters
        // the same cancel path (flag + deadline) and is killed once the grace
        // elapses on a later scan, exactly like an operator cancel.
        await FlagWallTimeOverdueAsync(ct);

        IReadOnlyList<Run> candidates;
        using (var scope = services.CreateScope())
        {
            candidates = await scope.ServiceProvider.GetRequiredService<RunRepository>()
                .GetCancelEnforcementCandidatesAsync(timeProvider.GetUtcNow(), ct);
        }
        var enforced = 0;
        foreach (var run in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (await EnforceAsync(run, ct)) enforced++;
        }
        return enforced;
    }

    // p0348: mark every RUNNING run past the ceiling cancel-requested (reason
    // watchdog-wall-time) with a kill deadline, synchronously in the DB so the
    // next scan does not re-flag it, and publish the event for dashboard fanout.
    // Only status="running" is a candidate: a "queued" (waiting for capacity) or
    // "waiting_for_input" (parked on a question) run is legitimately idle, not hung.
    private async Task FlagWallTimeOverdueAsync(CancellationToken ct)
    {
        var maxWallTime = CurrentMaxWallTime();
        if (maxWallTime <= TimeSpan.Zero) return;
        var now = timeProvider.GetUtcNow();

        using var scope = services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<RunRepository>();
        var overdue = await repo.GetWallTimeOverdueRunsAsync(maxWallTime, now, ct);
        foreach (var run in overdue)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning(
                "Run {RunId} exceeded wall-time ceiling {Ceiling}s (elapsed {Elapsed:F0}s) — requesting cancel",
                run.Id, maxWallTime.TotalSeconds, (now - run.StartedAt).TotalSeconds);
            await repo.MarkCancelRequestedAsync(run.Id, "watchdog-wall-time", now + KillGrace, ct);
            await events.PublishAsync(
                new RunCancelRequestedEvent(run.Id, "watchdog-wall-time", now), ct);
        }
    }

    private async Task<bool> EnforceAsync(Run run, CancellationToken ct)
    {
        logger.LogWarning(
            "Enforcing cancel for run {RunId} (job={JobId}, reason={Reason}) — grace elapsed",
            run.Id, run.JobId ?? "—", run.CancelReason ?? "operator");
        // The kill must land BEFORE the row is finalized: a terminate failure
        // (k8s API down) leaves the row non-terminal so the next scan retries —
        // finalizing first would mark the run cancelled while the pod keeps billing.
        if (!await TryTerminateAsync(run, ct)) return false;

        // p0355: the summary + ticket comment now reflect the TYPED reason on the row
        // (reap / wall-time / budget / operator …) instead of always reading "operator".
        await events.PublishAsync(new RunFinishedEvent(
            run.Id, "cancelled", null,
            CancelReasonNarrator.Summary(run.CancelReason),
            timeProvider.GetUtcNow()), ct);
        await ReleaseLeaseAsync(run, ct);
        // p0355: guard on current ownership — a run that a NEWER run has reclaimed
        // must not be finalized here (run.Id identifies the finalizing run).
        await ticketFinalizer.FinalizeAsync(run.Project, run.TicketId, run.Id,
            CancelReasonNarrator.TicketComment(run.CancelReason), ct);
        return true;
    }

    /// <summary>p0357 (p0330b): terminate retries are BOUNDED. Past this window after the
    /// kill deadline, an unkillable pod (k8s API down, job 404/gone) no longer blocks the
    /// finalize — the run must reach 'cancelled' in bounded time, never wedge in
    /// 'cancelling'. A row with no persisted deadline has been wedged already: no window.</summary>
    public static readonly TimeSpan TerminateRetryWindow = TimeSpan.FromMinutes(10);

    private async Task<bool> TryTerminateAsync(Run run, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(run.JobId)) return true; // in-process run: nothing spawned to kill
        var spawner = services.GetService<IJobSpawner>();
        if (spawner is null)
        {
            // No spawner in this composition — the job cannot exist here; finalize anyway
            // rather than wedging the run in 'cancelling' forever.
            logger.LogWarning("Run {RunId} has job {JobId} but no IJobSpawner is registered", run.Id, run.JobId);
            return true;
        }
        try
        {
            await spawner.TerminateAsync(run.JobId!, ct);
            return true;
        }
        catch (Exception ex)
        {
            if (IsPastRetryWindow(run))
            {
                logger.LogError(ex,
                    "Terminate failed for run {RunId} job {JobId} and the retry window elapsed — "
                    + "finalizing 'cancelled' anyway so the run reaches terminal", run.Id, run.JobId);
                return true;
            }
            logger.LogError(ex,
                "Terminate failed for run {RunId} job {JobId} — will retry next scan", run.Id, run.JobId);
            return false;
        }
    }

    private bool IsPastRetryWindow(Run run) =>
        run.CancelDeadlineAt is not { } deadline
        || timeProvider.GetUtcNow() - deadline > TerminateRetryWindow;

    private async Task ReleaseLeaseAsync(Run run, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(run.Project) || string.IsNullOrEmpty(run.TicketId)) return;
        await lease.ReleaseAsync(run.Project, new TicketId(run.TicketId), ct);
    }
}
