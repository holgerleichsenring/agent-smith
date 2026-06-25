using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0281a: routes a connection to its host's <see cref="IRepoDiscoveryProvider"/> and returns
/// the discovered repos. Throws when no provider matches the connection type.
/// </summary>
public interface IRepoDiscoveryService
{
    Task<IReadOnlyList<DiscoveredRepo>> DiscoverAsync(
        ResolvedConnection connection, CancellationToken cancellationToken);
}
