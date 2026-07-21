using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// p0358: watches the config epoch (same cheap Redis GET the poller leader uses)
/// and hands a change to <see cref="SkillsCatalogRefresher"/>. Runs on EVERY
/// replica — the skills cache is per-pod, so a leader-only refresh would leave
/// followers stale until their next run. Boot is a baseline, never a refresh
/// (preflight already resolved the catalog). In a no-Redis graph the Null signal
/// keeps the epoch constant and this service idles forever.
/// </summary>
public sealed class SkillsCatalogReloadHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    SkillsCatalogRefresher refresher,
    ILogger<SkillsCatalogReloadHostedService> logger) : BackgroundService
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

    private async Task<long?> WatchOnceAsync(long? baseline, CancellationToken ct)
    {
        try
        {
            var reload = services.GetRequiredService<IConfigReloadSignal>();
            var current = await reload.CurrentEpochAsync(ct);
            if (baseline is null) return current; // boot: preflight already resolved
            if (current == baseline) return baseline;

            logger.LogInformation(
                "Config epoch changed {From} -> {To} — checking skills catalog", baseline, current);
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            await refresher.RefreshAsync(config.Skills, ct);
            return current;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Skills epoch watch failed — retrying next interval");
            return baseline;
        }
    }
}
