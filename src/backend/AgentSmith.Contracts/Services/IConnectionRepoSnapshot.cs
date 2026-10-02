using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0281a: process-wide hot cache of the repos discovered per connection. The sync config
/// loader reads this to expand <c>connection/glob</c> references; the refresher writes it.
/// </summary>
public interface IConnectionRepoSnapshot
{
    /// <summary>
    /// The connection's repos and which store answered (2026-10-02-b540), or null when none holds
    /// any. Synchronous: the configuration loader expands globs on this read.
    /// </summary>
    ConnectionRepoSnapshotRead? TryRead(string connectionName);

    void Set(string connectionName, IReadOnlyList<DiscoveredRepo> repos);
}
