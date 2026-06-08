using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Periodically inspects the run-cancellation registry and cancels any
/// active run whose registered wall-time exceeds the configured ceiling.
/// Pattern mirrors <see cref="StaleJobDetector"/>: bare RunAsync loop with
/// a scan interval constant, no IHostedService coupling — the housekeeping
/// leader hosts it so a single replica scans.
/// </summary>
public sealed class PipelineRunWatchdog(
    IRunCancellationRegistry registry,
    IEventPublisher eventPublisher,
    int maxWallTimeSeconds,
    ILogger<PipelineRunWatchdog> logger)
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "PipelineRunWatchdog started (interval: {Interval}, max wall-time: {Seconds}s)",
            ScanInterval, maxWallTimeSeconds);
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
        var threshold = TimeSpan.FromSeconds(maxWallTimeSeconds);
        foreach (var entry in registry.Snapshot())
        {
            ct.ThrowIfCancellationRequested();
            if (now - entry.RegisteredAt < threshold) continue;
            await CancelOverdueAsync(entry, now - entry.RegisteredAt, ct);
        }
    }

    private async Task CancelOverdueAsync(
        RunCancellationEntry entry, TimeSpan elapsed, CancellationToken ct)
    {
        if (!registry.TryCancel(entry.RunId, reason: "watchdog-wall-time")) return;
        logger.LogWarning(
            "PipelineRunWatchdog cancelled run {RunId} after {Elapsed:F0}s (ceiling {Ceiling}s)",
            entry.RunId, elapsed.TotalSeconds, maxWallTimeSeconds);
        var evt = new RunCancelRequestedEvent(entry.RunId, "watchdog", DateTimeOffset.UtcNow);
        await eventPublisher.PublishAsync(evt, ct);
    }
}
