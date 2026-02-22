using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Configuration;
using AgentSmith.Infrastructure.Services.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure;

/// <summary>
/// Registers all infrastructure services (providers, factories, config) with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<SecretsProvider>();
        services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
        services.AddSingleton<ITicketProviderFactory, TicketProviderFactory>();
        services.AddSingleton<ISourceProviderFactory, SourceProviderFactory>();
        services.AddSingleton<IAgentProviderFactory, AgentProviderFactory>();
        services.AddSingleton<IProjectDetector, ProjectDetector>();
        services.AddSingleton<IContextValidator, ContextValidator>();
        services.AddSingleton<IContextGenerator>(sp =>
        {
            var secrets = sp.GetRequiredService<SecretsProvider>();
            var apiKey = secrets.GetOptional("ANTHROPIC_API_KEY") ?? "";
            var model = new ModelAssignment
            {
                Model = "claude-haiku-4-5-20251001",
                MaxTokens = 2048
            };
            var logger = sp.GetRequiredService<ILogger<ContextGenerator>>();
            return new ContextGenerator(apiKey, new RetryConfig(), model, logger);
        });
        services.AddSingleton<ICodeMapGenerator>(sp =>
        {
            var secrets = sp.GetRequiredService<SecretsProvider>();
            var apiKey = secrets.GetOptional("ANTHROPIC_API_KEY") ?? "";
            var model = new ModelAssignment
            {
                Model = "claude-haiku-4-5-20251001",
                MaxTokens = 4096
            };
            var logger = sp.GetRequiredService<ILogger<CodeMapGenerator>>();
            return new CodeMapGenerator(apiKey, new RetryConfig(), model, logger);
        });
        return services;
    }
}
