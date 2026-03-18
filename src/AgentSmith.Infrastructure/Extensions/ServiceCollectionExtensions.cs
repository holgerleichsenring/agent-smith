using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Output;
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

        // Output strategies (keyed by ProviderType for IOutputStrategy resolution)
        services.AddKeyedSingleton<IOutputStrategy, ConsoleOutputStrategy>("console");
        services.AddKeyedSingleton<IOutputStrategy, SarifOutputStrategy>("sarif");
        services.AddKeyedSingleton<IOutputStrategy, MarkdownOutputStrategy>("markdown");

        return services;
    }
}
