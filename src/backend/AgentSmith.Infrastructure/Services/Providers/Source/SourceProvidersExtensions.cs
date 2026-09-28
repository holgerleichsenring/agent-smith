using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Source-control providers: GitHub/AzDo client factories + the multi-provider
/// source factory + HostSourceCloner (Application uses for repo checkouts). PR
/// comment reply keyed by platform name.
/// </summary>
public static class SourceProvidersExtensions
{
    public static IServiceCollection AddSourceProviders(this IServiceCollection services)
    {
        services.AddSingleton<IGitHubClientFactory, DefaultGitHubClientFactory>();
        services.AddSingleton<IAzDoClientFactory, DefaultAzDoClientFactory>();
        services.AddSingleton<ISourceProviderFactory, SourceProviderFactory>();
        services.AddSingleton<IPrDiffProviderFactory, PrDiffProviderFactory>();
        services.AddSingleton<IHostSourceCloner, HostSourceCloner>();
        return services;
    }
}
