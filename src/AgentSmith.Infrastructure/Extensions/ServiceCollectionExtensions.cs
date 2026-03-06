using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Configuration;
using AgentSmith.Infrastructure.Services.Detection;
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
        services.AddHttpClient();
        services.AddSingleton<SecretsProvider>();
        services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
        services.AddSingleton<ITicketProviderFactory, TicketProviderFactory>();
        services.AddSingleton<ISourceProviderFactory, SourceProviderFactory>();
        services.AddSingleton<IAgentProviderFactory, AgentProviderFactory>();
        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();
        services.AddSingleton<ILanguageDetector, DotNetLanguageDetector>();
        services.AddSingleton<ILanguageDetector, TypeScriptLanguageDetector>();
        services.AddSingleton<ILanguageDetector, PythonLanguageDetector>();
        services.AddSingleton<IProjectDetector, ProjectDetector>();
        services.AddSingleton<IRepoSnapshotCollector, RepoSnapshotCollector>();
        services.AddSingleton<IContextValidator, ContextValidator>();
        services.AddSingleton<IContextGenerator, ContextGenerator>();
        services.AddSingleton<ICodeMapGenerator, CodeMapGenerator>();
        services.AddSingleton<ICodingPrinciplesGenerator, CodingPrinciplesGenerator>();
        services.AddSingleton<ISkillLoader, YamlSkillLoader>();
        return services;
    }
}
