using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Periodically inspects the run-cancellation registry and cancels any
/// active run whose registered wall-time exceeds the configured ceiling.
/// Pattern mirrors <see cref="EnqueuedReconciler"/>: bare RunAsync loop with
/// a scan interval constant, no IHostedService coupling — the housekeeping
/// leader hosts it so a single replica scans.
/// </summary>
public sealed class PipelineRunWatchdog(
    IRunCancellationRegistry registry,
    IEventPublisher eventPublisher,
    Func<int> maxWallTimeSeconds,
    ILogger<PipelineRunWatchdog> logger,
    IModelRunSlots? modelRunSlots = null) // 2026-11-20-24bd: a wait for a model place is no wall time
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // p0353: the ceiling is read LIVE each scan (not captured at construction), so a
        // Config Studio edit to orchestrator.max_run_wall_time_seconds takes effect on the
        // next scan without a restart.
        logger.LogInformation(
            "PipelineRunWatchdog started (interval: {Interval}, max wall-time: {Seconds}s)",
            ScanInterval, maxWallTimeSeconds());
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await ScanOnceAsync(cancellationToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "PipelineRunWatchdog scan failed"); }

            try { await Task.Delay(ScanInterval, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    // p0254: internal so tests exercise one deterministic scan directly instead of
    // racing the RunAsync loop against a short cancellation window (the flake).
    internal async Task ScanOnceAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var ceilingSeconds = maxWallTimeSeconds();
        var threshold = TimeSpan.FromSeconds(ceilingSeconds);
        var entries = registry.Snapshot();
        var waited = await WaitedAsync(entries, ct);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            // 2026-11-10-e706: a design turn is stopped by its cost cap or a person, never by its
            // age; every model wait it makes is bounded by the agent's network timeout instead.
            if (entry.Kind == RegisteredRunKind.DesignTurn) continue;
            var elapsed = now - entry.RegisteredAt - waited.GetValueOrDefault(entry.RunId);
            if (elapsed < threshold) continue;
            await CancelOverdueAsync(entry, elapsed, ceilingSeconds, ct);
        }
    }

    // 2026-11-20-24bd: a store that cannot be read counts no wait — the scan itself must still run.
    private async Task<IReadOnlyDictionary<string, TimeSpan>> WaitedAsync(
        IReadOnlyCollection<RunCancellationEntry> entries, CancellationToken ct)
    {
        if (modelRunSlots is null) return new Dictionary<string, TimeSpan>();
        try { return await modelRunSlots.WaitedAsync([.. entries.Select(e => e.RunId)], ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "PipelineRunWatchdog could not read model waits; scanning without them");
            return new Dictionary<string, TimeSpan>();
        }
    }

    private async Task CancelOverdueAsync(
        RunCancellationEntry entry, TimeSpan elapsed, int ceilingSeconds, CancellationToken ct)
    {
        if (!registry.TryCancel(entry.RunId, reason: "watchdog-wall-time")) return;
        logger.LogWarning(
            "PipelineRunWatchdog cancelled run {RunId} after {Elapsed:F0}s (ceiling {Ceiling}s)",
            entry.RunId, elapsed.TotalSeconds, ceilingSeconds);
        var evt = new RunCancelRequestedEvent(entry.RunId, "watchdog", DateTimeOffset.UtcNow);
        await eventPublisher.PublishAsync(evt, ct);
    }
}
