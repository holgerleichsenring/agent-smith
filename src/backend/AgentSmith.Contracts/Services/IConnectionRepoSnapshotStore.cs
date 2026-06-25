using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0281a: durable per-connection last-good repo set, surviving process restarts so a
/// discovery outage on a cold process still resolves from the last successful run. Mirrors
/// the p0182 store split (disk-backed; the file lives under the cache root).
/// </summary>
public interface IConnectionRepoSnapshotStore
{
    Task<IReadOnlyList<DiscoveredRepo>?> TryGetAsync(string connectionName, CancellationToken cancellationToken);

    Task SetAsync(string connectionName, IReadOnlyList<DiscoveredRepo> repos, CancellationToken cancellationToken);
}
