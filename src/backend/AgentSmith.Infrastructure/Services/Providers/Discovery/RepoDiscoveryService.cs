using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.Discovery;

/// <summary>
/// p0281a: routes a connection to the <see cref="IRepoDiscoveryProvider"/> registered for its
/// host type and returns the discovered repos.
/// </summary>
public sealed class RepoDiscoveryService : IRepoDiscoveryService
{
    private readonly IReadOnlyDictionary<RepoType, IRepoDiscoveryProvider> _providers;

    public RepoDiscoveryService(IEnumerable<IRepoDiscoveryProvider> providers) =>
        _providers = providers.ToDictionary(p => p.Type);

    public Task<IReadOnlyList<DiscoveredRepo>> DiscoverAsync(
        ResolvedConnection connection, CancellationToken cancellationToken)
    {
        if (!_providers.TryGetValue(connection.Type, out var provider))
            throw new Domain.Exceptions.ConfigurationException(
                $"Connection '{connection.Name}': no repo-discovery provider for type '{connection.Type}'.");
        return provider.DiscoverAsync(connection, cancellationToken);
    }
}
