using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Discovery;

/// <summary>
/// p0281a: refreshes the per-connection repo snapshot. Live discovery success updates the hot
/// snapshot AND the durable last-good store. On discovery failure it falls back to the durable
/// last-good (stale-warned); a connection with no prior snapshot (cold) fails loud so a run
/// never silently operates on an empty repo set.
/// </summary>
public sealed class RepoDiscoveryRefresher(
    IRepoDiscoveryService discovery,
    IConnectionRepoSnapshot snapshot,
    IConnectionRepoSnapshotStore store,
    ILogger<RepoDiscoveryRefresher> logger) : IRepoDiscoveryRefresher
{
    public async Task RefreshAsync(ResolvedConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var repos = await discovery.DiscoverAsync(connection, cancellationToken);
            snapshot.Set(connection.Name, repos);
            await store.SetAsync(connection.Name, repos, cancellationToken);
            logger.LogInformation(
                "RepoDiscovery: connection '{Connection}' discovered {Count} repo(s).", connection.Name, repos.Count);
        }
        catch (Exception ex) when (ex is not ConfigurationException)
        {
            await FallBackToLastGoodAsync(connection, ex, cancellationToken);
        }
    }

    public async Task RefreshAllAsync(
        IReadOnlyCollection<ResolvedConnection> connections, CancellationToken cancellationToken)
    {
        foreach (var connection in connections)
        {
            try
            {
                await RefreshAsync(connection, cancellationToken);
            }
            catch (ConfigurationException ex)
            {
                // One cold connection must not abort the whole refresh sweep.
                logger.LogError(ex, "RepoDiscovery: refresh failed for connection '{Connection}'.", connection.Name);
            }
        }
    }

    private async Task FallBackToLastGoodAsync(
        ResolvedConnection connection, Exception cause, CancellationToken cancellationToken)
    {
        var lastGood = await store.TryGetAsync(connection.Name, cancellationToken);
        if (lastGood is not null)
        {
            snapshot.Set(connection.Name, lastGood);
            logger.LogWarning(cause,
                "RepoDiscovery: discovery FAILED for connection '{Connection}' — serving the last-good " +
                "snapshot ({Count} repo(s), possibly STALE).", connection.Name, lastGood.Count);
            return;
        }

        throw new ConfigurationException(
            $"Connection '{connection.Name}': repo discovery failed and no last-good snapshot exists (cold cache). " +
            $"Fix the connection credentials/reachability before the run can resolve its repos. Cause: {cause.Message}");
    }
}
