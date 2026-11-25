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
        // 2026-11-25-9992h: a GitHub App machine identity's installation tokens, one synchronous mint an hour per installation.
        // 2026-11-25-d0e2: and a service principal's Entra tokens, one synchronous mint an hour per principal.
        services.AddSingleton<IMachineTokens>(_ => new MachineTokens(
            new GitHubAppTokens(new HttpClient(), TimeProvider.System), new EntraPrincipalTokens(new HttpClient(), TimeProvider.System)));
        services.AddSingleton<IPrDiffProviderFactory, PrDiffProviderFactory>();
        services.AddSingleton<IHostSourceCloner, HostSourceCloner>();
        services.AddSingleton<IPipelineRunnerFactory, PipelineRunnerFactory>(); // 2026-11-18-3863e
        services.AddSingleton<AgentSmith.Contracts.Reviews.IPullRequestCiTriggerFactory, PullRequestCiTriggerFactory>(); // 2026-11-24-f9c5d
        services.AddSingleton<IContainerRegistryFactory, ContainerRegistryFactory>(); // 2026-11-23-933de
        services.AddHttpClient(ContainerRegistryFactory.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<IArgoCdClientFactory, ArgoCdClientFactory>(); // 2026-11-23-c81ec
        services.AddHttpClient(ArgoCdClientFactory.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        return services;
    }
}
