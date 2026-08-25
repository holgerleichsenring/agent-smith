using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// 2026-08-25-1806: watches the config epoch (the same cheap Redis GET the poller leader
/// uses) and reloads the config store's assembled document. Runs on EVERY replica, because
/// the store's document is per-pod: a save lands on one replica and reloads only that one,
/// so the replica the next request hits would still be answering from the document it
/// assembled at boot.
/// <para>
/// Boot is a baseline, never a reload — the store loads its own document on first use. In a
/// no-Redis graph the Null signal keeps the epoch constant and this service idles forever,
/// which is correct: a single process reloads its store on its own writes.
/// </para>
/// </summary>
public sealed class ConfigStoreReloadHostedService(
    IServiceProvider services,
    IConfigStore store,
    ILogger<ConfigStoreReloadHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan EpochWatchInterval = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long? baseline = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            baseline = await WatchOnceAsync(baseline, stoppingToken);
            try { await Task.Delay(EpochWatchInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<long?> WatchOnceAsync(long? baseline, CancellationToken cancellationToken)
    {
        try
        {
            var current = await services.GetRequiredService<IConfigReloadSignal>()
                .CurrentEpochAsync(cancellationToken);
            if (baseline is null || current == baseline) return current;

            logger.LogInformation("Config epoch changed {From} -> {To} — reloading the config store",
                baseline, current);
            store.Load();
            return current;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Config store epoch watch failed — retrying next interval");
            return baseline;
        }
    }
}
