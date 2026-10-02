using System.Text.Json;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5ab2b: recovers what Redis lost of a stored request. An unclaimed request pushed
/// over <see cref="LostAfter"/> ago never reached a consumer — it is pushed again from its row,
/// context and all, and its push time renewed; a lost entry therefore runs within 90 s. A
/// claimed request whose row has not beaten for <see cref="RunLivenessReaper.StaleAfter"/> lost
/// its consumer before RunStarted — it is flagged cancel reason interrupted, as the liveness
/// reaper flags a running row, and CancelEnforcer ends it. Under the housekeeping leader; a
/// suspend gap suppresses both verdicts for one window.
/// </summary>
public sealed class QueuedRunSweeper(
    IServiceScopeFactory scopes,
    IRedisJobQueue jobQueue,
    IEventPublisher events,
    TimeProvider timeProvider,
    ILogger<QueuedRunSweeper> logger)
{
    public static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan LostAfter = TimeSpan.FromSeconds(60);

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<QueuedRunRepository>();
        var now = timeProvider.GetUtcNow();
        foreach (var run in await repository.GetUnclaimedAsync(LostAfter, now, ct))
            await PushAgainAsync(repository, run, now, ct);
        foreach (var run in await repository.GetDeadClaimsAsync(RunLivenessReaper.StaleAfter, now, ct))
            await FlagInterruptedAsync(scope.ServiceProvider.GetRequiredService<RunRepository>(), run, now, ct);
    }

    public async Task RunAsync(TimeSpan scanInterval, CancellationToken ct)
    {
        logger.LogInformation("QueuedRunSweeper started (lost>{Lost}, dead claim>{Stale}, scan {Scan})",
            LostAfter, RunLivenessReaper.StaleAfter, scanInterval);
        var gaps = new SuspendGapDetector(timeProvider, scanInterval, RunLivenessReaper.StaleAfter);
        while (!ct.IsCancellationRequested)
        {
            if (gaps.Observe() is { } gap)
                logger.LogWarning("QueuedRunSweeper detected a suspend gap of {Gap} — no verdicts for {Grace}",
                    gap, RunLivenessReaper.StaleAfter);
            if (!gaps.SuppressesVerdicts())
            {
                try { await RunOnceAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { logger.LogError(ex, "QueuedRunSweeper scan failed"); }
            }
            try { await Task.Delay(scanInterval, timeProvider, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PushAgainAsync(QueuedRunRepository repository, Run run, DateTimeOffset now, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<PipelineRequest>(run.QueuedRequestJson!)!;
        await jobQueue.EnqueueAsync(request, ct);
        await repository.RenewEnqueuedAsync(run.Id, now, ct);
        logger.LogWarning("Run {RunId} was queued {Since} ago and never claimed — its request is pushed again",
            run.Id, now - run.RequestEnqueuedAt);
    }

    private async Task FlagInterruptedAsync(RunRepository runs, Run run, DateTimeOffset now, CancellationToken ct)
    {
        logger.LogWarning("Run {RunId} was claimed at {Claimed} and its consumer stopped beating — ending it as interrupted",
            run.Id, run.ClaimedAt);
        if (!await runs.MarkCancelRequestedAsync(run.Id, RunLivenessReaper.InterruptedReason, now, ct)) return;
        await events.PublishAsync(new RunCancelRequestedEvent(run.Id, RunLivenessReaper.InterruptedReason, now), ct);
    }
}
