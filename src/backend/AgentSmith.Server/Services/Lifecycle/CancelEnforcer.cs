using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;

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
    /// <summary>Grace between the persisted cancel and the force-kill — the
    /// window in which a cooperative (in-process) cancel may land first.</summary>
    public static readonly TimeSpan KillGrace = TimeSpan.FromSeconds(30);
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

    private async Task<bool> EnforceAsync(Run run, CancellationToken ct)
    {
        logger.LogWarning(
            "Enforcing cancel for run {RunId} (job={JobId}, reason={Reason}) — grace elapsed",
            run.Id, run.JobId ?? "—", run.CancelReason ?? "operator");
        // The kill must land BEFORE the row is finalized: a terminate failure
        // (k8s API down) leaves the row non-terminal so the next scan retries —
        // finalizing first would mark the run cancelled while the pod keeps billing.
        if (!await TryTerminateAsync(run, ct)) return false;

        await events.PublishAsync(new RunFinishedEvent(
            run.Id, "cancelled", null,
            "Cancelled by operator — enforced after the grace period.",
            timeProvider.GetUtcNow()), ct);
        await ReleaseLeaseAsync(run, ct);
        await ticketFinalizer.FinalizeAsync(run.Project, run.TicketId,
            "<b>Agent Smith — Cancelled</b><br/>Cancelled by operator.", ct);
        return true;
    }

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
            logger.LogError(ex,
                "Terminate failed for run {RunId} job {JobId} — will retry next scan", run.Id, run.JobId);
            return false;
        }
    }

    private async Task ReleaseLeaseAsync(Run run, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(run.Project) || string.IsNullOrEmpty(run.TicketId)) return;
        await lease.ReleaseAsync(run.Project, new TicketId(run.TicketId), ct);
    }
}
