using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Detection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core;

/// <summary>
/// Registers non-provider infrastructure services (config, detection, generation) with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithCore(this IServiceCollection services)
    {
        services.AddSingleton<SecretsProvider>();
        services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
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
        services.AddSingleton<IDecisionLogger, FileDecisionLogger>();
        services.AddSingleton<IPromptTemplateProvider>(sp =>
            new FilePromptTemplateProvider(
                Path.Combine("config", "prompts"),
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<FilePromptTemplateProvider>()));
        return services;
    }
}
