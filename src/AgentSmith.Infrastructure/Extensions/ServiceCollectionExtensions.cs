using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core;
using AgentSmith.Infrastructure.Services.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure;

/// <summary>
/// Registers all infrastructure services (providers, factories, config) with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithInfrastructure(this IServiceCollection services)
    {
        services.AddAgentSmithCore();
        services.AddHttpClient();
        services.AddSingleton<ITicketProviderFactory, TicketProviderFactory>();
        services.AddSingleton<ISourceProviderFactory, SourceProviderFactory>();
        services.AddSingleton<IAgentProviderFactory, AgentProviderFactory>();
        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();
        return services;
    }
}
