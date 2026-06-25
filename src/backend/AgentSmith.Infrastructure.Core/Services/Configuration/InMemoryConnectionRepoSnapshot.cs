using System.Collections.Concurrent;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281a: process-wide hot cache of discovered repos per connection. Singleton; the
/// refresher writes it (live discovery or durable last-good), the sync glob expander reads it.
/// </summary>
public sealed class InMemoryConnectionRepoSnapshot : IConnectionRepoSnapshot
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<DiscoveredRepo>> _byConnection = new();

    public bool TryGet(string connectionName, out IReadOnlyList<DiscoveredRepo> repos) =>
        _byConnection.TryGetValue(connectionName, out repos!);

    public void Set(string connectionName, IReadOnlyList<DiscoveredRepo> repos) =>
        _byConnection[connectionName] = repos;
}
