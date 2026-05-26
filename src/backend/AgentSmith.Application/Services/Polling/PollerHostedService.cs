using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// p0140c: drives per-tracker pollers. Each poll cycle invokes every IEventPoller in
/// parallel with a per-poller 20s timeout, then sleeps until the next cycle. Pollers now
/// own their spawn path (they call ISpawnPipelineRunsUseCase directly during PollAsync),
/// so this service only orchestrates the loop + logs PollResult summaries — it no longer
/// collects ClaimRequests or invokes ITicketClaimService.
/// </summary>
public sealed class PollerHostedService(
    IEnumerable<IEventPoller> pollers,
    ILogger<PollerHostedService> logger)
{
    private static readonly TimeSpan PerPollerTimeout = TimeSpan.FromSeconds(20);
    private readonly Random _jitterRng = new();

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var configured = pollers.ToList();
        if (configured.Count == 0)
        {
            logger.LogInformation("PollerHostedService: no pollers configured, idling");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return;
        }

        logger.LogInformation(
            "PollerHostedService started with {Count} per-tracker poller(s)", configured.Count);

        while (!cancellationToken.IsCancellationRequested)
        {
            await CycleAsync(configured, cancellationToken);
            await SleepAsync(configured, cancellationToken);
        }
    }

    private async Task CycleAsync(IReadOnlyList<IEventPoller> pollers, CancellationToken ct)
        => await Task.WhenAll(pollers.Select(p => PollSafeAsync(p, ct)));

    private async Task PollSafeAsync(IEventPoller poller, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(PerPollerTimeout);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("Polling {Platform} tracker '{Tracker}'…", poller.PlatformName, poller.TrackerName);
        try
        {
            var result = await poller.PollAsync(cts.Token);
            LogResult(poller, result, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Poller {Platform}/{Tracker} timed out after {Ms}ms",
                poller.PlatformName, poller.TrackerName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Poller {Platform}/{Tracker} failed after {Ms}ms",
                poller.PlatformName, poller.TrackerName, sw.ElapsedMilliseconds);
        }
    }

    private void LogResult(IEventPoller poller, PollResult result, long elapsedMs)
        => logger.LogInformation(
            "Polled {Platform}/{Tracker}: {Polled} polled, {Matched} matched, {Spawned} spawned in {Ms}ms",
            poller.PlatformName, poller.TrackerName,
            result.PolledTickets, result.MatchedProjects, result.Spawned, elapsedMs);

    private async Task SleepAsync(IReadOnlyList<IEventPoller> pollers, CancellationToken ct)
    {
        var minInterval = pollers.Min(p => p.IntervalSeconds);
        var jitter = (_jitterRng.NextDouble() - 0.5) * 0.2; // ±10%
        var sleep = TimeSpan.FromSeconds(minInterval * (1 + jitter));
        try { await Task.Delay(sleep, ct); }
        catch (OperationCanceledException) { /* shutdown */ }
    }
}
