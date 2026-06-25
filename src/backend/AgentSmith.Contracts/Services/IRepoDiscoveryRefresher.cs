using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0281a: refreshes the in-memory repo snapshot for connections. Live discovery success
/// updates the hot snapshot + the durable last-good store; on failure the durable last-good
/// is loaded into the snapshot (stale-warned). A connection with no prior snapshot (cold
/// cache) fails loud rather than running on an empty repo set.
/// </summary>
public interface IRepoDiscoveryRefresher
{
    /// <summary>Refresh a single connection; throws on cold-cache discovery failure.</summary>
    Task RefreshAsync(ResolvedConnection connection, CancellationToken cancellationToken);

    /// <summary>Refresh every connection; a single connection's failure does not abort the rest.</summary>
    Task RefreshAllAsync(IReadOnlyCollection<ResolvedConnection> connections, CancellationToken cancellationToken);
}
