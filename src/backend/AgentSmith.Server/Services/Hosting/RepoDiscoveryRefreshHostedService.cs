using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// p0281a (refresh trigger = background interval + warm-on-start): keeps the per-connection
/// repo snapshot fresh so a repo added under a connection appears without a restart, and the
/// snapshot is warmed before the first webhook needs it. The sync config loader reads the
/// snapshot; this service writes it out-of-band.
/// </summary>
public sealed class RepoDiscoveryRefreshHostedService(
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    IRepoDiscoveryRefresher refresher,
    ILogger<RepoDiscoveryRefreshHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately (warm-on-start), then on the interval.
        do
        {
            await RefreshOnceAsync(stoppingToken);
        }
        while (await WaitAsync(stoppingToken));
    }

    private async Task RefreshOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connections = configLoader.LoadConfig(serverContext.ConfigPath).Connections.Values.ToList();
            if (connections.Count == 0) return;
            await refresher.RefreshAllAsync(connections, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "RepoDiscoveryRefreshHostedService: refresh sweep failed; retrying next interval.");
        }
    }

    private static async Task<bool> WaitAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(RefreshInterval, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
