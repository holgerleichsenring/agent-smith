using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0281a: process-wide hot cache of the repos discovered per connection. The sync config
/// loader reads this to expand <c>connection/glob</c> references; the refresher writes it.
/// </summary>
public interface IConnectionRepoSnapshot
{
    bool TryGet(string connectionName, out IReadOnlyList<DiscoveredRepo> repos);

    void Set(string connectionName, IReadOnlyList<DiscoveredRepo> repos);
}
