using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0281a: lists the repositories under a <see cref="ResolvedConnection"/> via one git
/// host's API. One implementation per host kind (azure_devops / github / gitlab), selected
/// by <see cref="Type"/>.
/// </summary>
public interface IRepoDiscoveryProvider
{
    RepoType Type { get; }

    Task<IReadOnlyList<DiscoveredRepo>> DiscoverAsync(
        ResolvedConnection connection, CancellationToken cancellationToken);
}
