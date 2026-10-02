using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ConnectionRefreshFlights>(); // 2026-10-02-5f89c
        services.AddSingleton<IRepoDiscoveryRefresher, RepoDiscoveryRefresher>();
        return services;
    }
}
