using AgentSmith.Contracts.Exceptions;
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
/// 2026-10-02-5f89c: every outcome is recorded in the store, so the studio shows the last error
/// beside the last success. A missing secret is an outage like a refused token — the repos found
/// yesterday are still the repos; any other configuration error still throws once recorded.
/// One refresh per connection at a time (<see cref="ConnectionRefreshFlights"/>).
/// </summary>
public sealed class RepoDiscoveryRefresher(
    IRepoDiscoveryService discovery,
    IConnectionRepoSnapshot snapshot,
    IConnectionRepoSnapshotStore store,
    ConnectionRefreshFlights flights,
    TimeProvider clock,
    ILogger<RepoDiscoveryRefresher> logger) : IRepoDiscoveryRefresher
{
    private const int ErrorBound = 300;

    public Task RefreshAsync(ResolvedConnection connection, CancellationToken cancellationToken) =>
        flights.RunAsync(connection.Name, () => RefreshNowAsync(connection), cancellationToken);

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

    // The shared refresh outlives any one waiter, so it runs on no caller's token.
    private async Task RefreshNowAsync(ResolvedConnection connection)
    {
        try
        {
            var repos = await discovery.DiscoverAsync(connection, CancellationToken.None);
            await store.SetAsync(connection.Name, repos, clock.GetUtcNow(), CancellationToken.None);
            snapshot.Set(connection.Name, repos);
            logger.LogInformation(
                "RepoDiscovery: connection '{Connection}' discovered {Count} repo(s).", connection.Name, repos.Count);
        }
        catch (Exception ex) when (ex is MissingCredentialException || ex is not ConfigurationException)
        {
            await RecordAsync(connection, ex);
            await FallBackToLastGoodAsync(connection, ex);
        }
        catch (ConfigurationException ex)
        {
            await RecordAsync(connection, ex);
            throw;
        }
    }

    private Task RecordAsync(ResolvedConnection connection, Exception cause) =>
        store.RecordFailureAsync(connection.Name,
            cause.Message.Length <= ErrorBound ? cause.Message : cause.Message[..ErrorBound] + "…",
            clock.GetUtcNow(), CancellationToken.None);

    private async Task FallBackToLastGoodAsync(ResolvedConnection connection, Exception cause)
    {
        var lastGood = await store.TryGetAsync(connection.Name, CancellationToken.None);
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
