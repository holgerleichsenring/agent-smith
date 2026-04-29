using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Fan-out all configured IEventPollers in parallel with per-poller 20s timeout,
/// then serialise ClaimAsync calls per cycle. Jitter is per-project to avoid
/// alignment across projects. Called as the work callback of a LeaderElectedHostedService.
/// </summary>
public sealed class PollerHostedService(
    IEnumerable<IEventPoller> pollers,
    ITicketClaimService claimService,
    IConfigurationLoader configLoader,
    string configPath,
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
            "PollerHostedService started with {Count} pollers", configured.Count);

        while (!cancellationToken.IsCancellationRequested)
        {
            await CycleAsync(configured, cancellationToken);
            await SleepAsync(configured, cancellationToken);
        }
    }

    private async Task CycleAsync(IReadOnlyList<IEventPoller> pollers, CancellationToken ct)
    {
        var results = await Task.WhenAll(pollers.Select(p => PollSafeAsync(p, ct)));
        var config = configLoader.LoadConfig(configPath);
        foreach (var requests in results)
            foreach (var request in requests)
                await ProcessClaimAsync(request, config, ct);
    }

    private async Task<IReadOnlyList<ClaimRequest>> PollSafeAsync(
        IEventPoller poller, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(PerPollerTimeout);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("Polling {Platform}/{Project}…", poller.PlatformName, poller.ProjectName);
        try
        {
            var result = await poller.PollAsync(cts.Token);
            logger.LogInformation(
                "Polled {Platform}/{Project}: {Count} candidate(s) in {Ms}ms",
                poller.PlatformName, poller.ProjectName, result.Count, sw.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Poller {Platform}/{Project} timed out after {Ms}ms",
                poller.PlatformName, poller.ProjectName, sw.ElapsedMilliseconds);
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Poller {Platform}/{Project} failed after {Ms}ms",
                poller.PlatformName, poller.ProjectName, sw.ElapsedMilliseconds);
            return [];
        }
    }

    private async Task ProcessClaimAsync(
        ClaimRequest request, AgentSmithConfig config, CancellationToken ct)
    {
        try
        {
            var result = await claimService.ClaimAsync(request, config, ct);
            logger.LogDebug("Poll-claim {Outcome}: {Project}/{Ticket}",
                result.Outcome, request.ProjectName, request.TicketId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ClaimAsync failed for polled candidate {Project}/{Ticket}",
                request.ProjectName, request.TicketId.Value);
        }
    }

    private async Task SleepAsync(IReadOnlyList<IEventPoller> pollers, CancellationToken ct)
    {
        var minInterval = pollers.Min(p => p.IntervalSeconds);
        var jitter = (_jitterRng.NextDouble() - 0.5) * 0.2; // ±10%
        var sleep = TimeSpan.FromSeconds(minInterval * (1 + jitter));
        try { await Task.Delay(sleep, ct); }
        catch (OperationCanceledException) { /* shutdown */ }
    }
}
