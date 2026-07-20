using AgentSmith.Application.Services.Health;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// Background-service wrapper that runs <see cref="PollerHostedService"/>
/// under leader election (lease key 'agentsmith:leader:poller') with the
/// redis-gated retry loop.
/// </summary>
public sealed class PollerLeaderHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    ILogger<PollerLeaderHostedService> logger) : BackgroundService
{
    private const string LeaseKey = "agentsmith:leader:poller";

    // p0353: how often the leader polls the config epoch between poll cycles.
    // A cheap Redis GET; the reload latency after an import is at most this.
    private static readonly TimeSpan EpochWatchInterval = TimeSpan.FromSeconds(3);

    private readonly SubsystemHealth _health = new("poller");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PollerLeaderHostedService.ExecuteAsync entered (lease key: {Key})", LeaseKey);
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(
            services, _health, LeaseKey, RunPollerAsync, retry, stoppingToken);
    }

    // p0353: the leader holds its lease and rebuilds its poller list IN PLACE when
    // the config epoch advances (an import / edit on any replica bumped it). The lease
    // is never released — no failover, no double-poll window. In a no-Redis graph the
    // Null signal keeps the epoch at 0, the watcher never fires, and this collapses to
    // the pre-p0353 single-build-runs-until-shutdown behaviour.
    private async Task RunPollerAsync(CancellationToken ct)
    {
        var reload = services.GetRequiredService<IConfigReloadSignal>();
        var systemEvents = services.GetRequiredService<ISystemEventPublisher>();
        var epoch = await reload.CurrentEpochAsync(ct);
        logger.LogInformation("RunPollerAsync entered at config epoch {Epoch}", epoch);

        while (!ct.IsCancellationRequested)
        {
            using var reloadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var scope = services.CreateScope();
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            logger.LogInformation(
                "Building pollers from {TrackerCount} trackers (epoch {Epoch})", config.Trackers.Count, epoch);
            var host = new PollerHostedService(
                PollerFactory.Build(services, config),
                systemEvents,
                services.GetRequiredService<ILogger<PollerHostedService>>());

            var watcher = WatchEpochAsync(reload, epoch, reloadCts);
            try
            {
                await host.RunAsync(reloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Reload requested or shutdown — the idle (no-pollers) path throws here.
            }
            finally
            {
                if (!reloadCts.IsCancellationRequested) reloadCts.Cancel();
                await watcher;
            }

            if (ct.IsCancellationRequested) break; // real shutdown / lease loss

            // The epoch advanced: re-read the newest value (a burst of imports collapses
            // into one rebuild) and rebuild from fresh config, lease still held.
            epoch = await reload.CurrentEpochAsync(ct);
            logger.LogInformation("Config epoch advanced to {Epoch} — rebuilding pollers in place", epoch);
            await TryPublishReloadedAsync(systemEvents, epoch, config.Trackers.Count, ct);
        }

        logger.LogInformation("RunPollerAsync exiting (cancellation or lease loss)");
    }

    // Polls the epoch on a cheap interval; cancels the run's linked CTS when it advances,
    // which unwinds host.RunAsync so the outer loop rebuilds. Delays on reloadCts.Token
    // so a shutdown / finally-cancel unblocks it promptly (no up-to-interval lag).
    private async Task WatchEpochAsync(IConfigReloadSignal reload, long fromEpoch, CancellationTokenSource reloadCts)
    {
        try
        {
            while (!reloadCts.IsCancellationRequested)
            {
                await Task.Delay(EpochWatchInterval, reloadCts.Token);
                long current;
                try { current = await reload.CurrentEpochAsync(reloadCts.Token); }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { logger.LogDebug(ex, "Config epoch read failed — treating as no change"); continue; }
                if (current != fromEpoch)
                {
                    logger.LogInformation("Config epoch changed {From} -> {To} — signalling poller rebuild", fromEpoch, current);
                    if (!reloadCts.IsCancellationRequested) reloadCts.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown or host exited */ }
    }

    private async Task TryPublishReloadedAsync(ISystemEventPublisher systemEvents, long epoch, int trackerCount, CancellationToken ct)
    {
        try { await systemEvents.PublishAsync(new ConfigReloadedEvent("poller", epoch, trackerCount, DateTimeOffset.UtcNow), ct); }
        catch (Exception ex) { logger.LogDebug(ex, "Failed to publish ConfigReloadedEvent for epoch {Epoch}", epoch); }
    }
}
