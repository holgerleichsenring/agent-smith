using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// p0353 / 2026-10-08-9e6e: a leader's work rebuilt IN PLACE whenever the config epoch advances —
/// the lease is held throughout, so there is no failover and no double run. Extracted from the
/// poller leader so the change sweep follows the same config the same way. In a no-Redis graph the
/// epoch stays 0 and the work simply runs until shutdown.
/// </summary>
public sealed class LeaderEpochLoop(
    IConfigReloadSignal reload,
    ISystemEventPublisher systemEvents,
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<LeaderEpochLoop> logger)
{
    private static readonly TimeSpan EpochWatchInterval = TimeSpan.FromSeconds(3);

    public async Task RunAsync(string subsystem, Func<AgentSmithConfig, CancellationToken, Task> work, CancellationToken ct)
    {
        var epoch = await reload.CurrentEpochAsync(ct);
        while (!ct.IsCancellationRequested)
        {
            using var reloadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var watcher = WatchEpochAsync(subsystem, epoch, reloadCts);
            try
            {
                await work(config, reloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Reload requested or shutdown.
            }
            finally
            {
                if (!reloadCts.IsCancellationRequested) reloadCts.Cancel();
                await watcher;
            }
            if (ct.IsCancellationRequested) break;
            epoch = await reload.CurrentEpochAsync(ct);
            logger.LogInformation("Config epoch advanced to {Epoch} — rebuilding {Subsystem} in place", epoch, subsystem);
            await TryPublishReloadedAsync(subsystem, epoch, config.Trackers.Count, ct);
        }
    }

    // Polls the epoch on a cheap interval; cancels the run's linked CTS when it advances.
    private async Task WatchEpochAsync(string subsystem, long fromEpoch, CancellationTokenSource reloadCts)
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
                if (current == fromEpoch) continue;
                logger.LogInformation("Config epoch changed {From} -> {To} — signalling {Subsystem} rebuild", fromEpoch, current, subsystem);
                if (!reloadCts.IsCancellationRequested) reloadCts.Cancel();
                return;
            }
        }
        catch (OperationCanceledException) { /* shutdown or work exited */ }
    }

    private async Task TryPublishReloadedAsync(string subsystem, long epoch, int trackerCount, CancellationToken ct)
    {
        try { await systemEvents.PublishAsync(new ConfigReloadedEvent(subsystem, epoch, trackerCount, DateTimeOffset.UtcNow), ct); }
        catch (Exception ex) { logger.LogDebug(ex, "Failed to publish ConfigReloadedEvent for epoch {Epoch}", epoch); }
    }
}
