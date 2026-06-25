using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Providers.Discovery;

/// <summary>
/// p0281a: registers the per-host repo-discovery providers (azure_devops / github / gitlab),
/// the routing service, and the refresher that keeps the connection repo snapshot warm.
/// </summary>
public static class DiscoveryProvidersExtensions
{
    public static IServiceCollection AddRepoDiscovery(this IServiceCollection services)
    {
        services.AddSingleton<IRepoDiscoveryProvider, AzureDevOpsRepoDiscoveryProvider>();
        services.AddSingleton<IRepoDiscoveryProvider, GitHubRepoDiscoveryProvider>();
        services.AddSingleton<IRepoDiscoveryProvider, GitLabRepoDiscoveryProvider>();
        services.AddSingleton<IRepoDiscoveryService, RepoDiscoveryService>();
        services.AddSingleton<IRepoDiscoveryRefresher, RepoDiscoveryRefresher>();
        return services;
    }
}
