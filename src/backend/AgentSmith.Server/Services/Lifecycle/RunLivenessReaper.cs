using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5f89e: ends a run no process drives any more. A restarted server's in-memory
/// cancellation registry starts empty, so PipelineRunWatchdog never sees the run; a
/// ticketless run holds no lease, so ActiveRunReaper never sees it; and the wall-time
/// backstop fires only after max_run_wall_time_seconds. The run row's own heartbeat is the
/// signal here: a running row whose last beat is older than <see cref="StaleAfter"/> is
/// flagged cancel-requested with reason <see cref="InterruptedReason"/> and deadline now,
/// exactly as a wall-time verdict is, and CancelEnforcer ends it on its next scan — which
/// takes it out of the active-runs set, so the sandbox reaper removes its sandboxes.
/// <para>
/// Runs on every replica. A run replica B drives renews its own row, so replica A reads a
/// fresh beat and leaves it. A run alive in THIS process is a lagging pump, not a dead one —
/// refreshed, never flagged. After a suspend every beat is stale by construction, so a
/// detected gap suppresses verdicts for one window. The DB, never Redis: an empty Redis
/// must not read as every run dead.
/// </para>
/// </summary>
public sealed class RunLivenessReaper(
    IServiceScopeFactory scopes,
    IRunCancellationRegistry cancellationRegistry,
    IRunHeartbeat heartbeat,
    IEventPublisher events,
    TimeProvider timeProvider,
    ILogger<RunLivenessReaper> logger)
{
    public const string InterruptedReason = "interrupted";

    /// <summary>Four missed 45 s beats — the lease's own freshness bound.</summary>
    public static readonly TimeSpan StaleAfter = ActiveRunReaper.LeaseFreshFor;

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var now = timeProvider.GetUtcNow();
        var stale = await scope.ServiceProvider.GetRequiredService<RunLivenessRepository>()
            .GetStaleRunningRunsAsync(StaleAfter, now, ct);
        var flagged = 0;
        foreach (var run in stale)
        {
            ct.ThrowIfCancellationRequested();
            if (cancellationRegistry.IsLocallyActive(run.Id)) await RefreshAsync(run, ct);
            else if (await FlagInterruptedAsync(scope.ServiceProvider, run, now, ct)) flagged++;
        }
        return flagged;
    }

    public async Task RunAsync(TimeSpan scanInterval, CancellationToken ct)
    {
        logger.LogInformation("RunLivenessReaper started (stale>{Stale}, scan {Scan})", StaleAfter, scanInterval);
        var gaps = new SuspendGapDetector(timeProvider, scanInterval, StaleAfter);
        while (!ct.IsCancellationRequested)
        {
            if (gaps.Observe() is { } gap)
                logger.LogWarning("RunLivenessReaper detected a suspend gap of {Gap} — no verdicts for {Grace}", gap, StaleAfter);
            if (!gaps.SuppressesVerdicts())
            {
                try { await RunOnceAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { logger.LogError(ex, "RunLivenessReaper scan failed"); }
            }
            try { await Task.Delay(scanInterval, timeProvider, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RefreshAsync(Run run, CancellationToken ct)
    {
        await heartbeat.RenewAsync(run.Id, ct);
        logger.LogWarning("Spared run {RunId}: alive in this process — beat refreshed (the heartbeat pump is behind)", run.Id);
    }

    private async Task<bool> FlagInterruptedAsync(
        IServiceProvider scoped, Run run, DateTimeOffset now, CancellationToken ct)
    {
        logger.LogWarning(
            "Run {RunId} has no driving process (last beat {Beat}, started {Started}) — ending it as interrupted",
            run.Id, run.HeartbeatAt, run.StartedAt);
        if (!await scoped.GetRequiredService<RunRepository>().MarkCancelRequestedAsync(run.Id, InterruptedReason, now, ct))
            return false;
        await events.PublishAsync(new RunCancelRequestedEvent(run.Id, InterruptedReason, now), ct);
        return true;
    }
}
