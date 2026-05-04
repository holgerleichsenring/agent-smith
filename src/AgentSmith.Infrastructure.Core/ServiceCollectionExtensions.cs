using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Detection;
using AgentSmith.Infrastructure.Core.Services.Skills;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Core;

/// <summary>
/// Registers non-provider infrastructure services (config, detection, generation) with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithCore(this IServiceCollection services)
    {
        services.AddSingleton<SecretsProvider>();
        services.AddSingleton<ProjectConfigNormalizer>();
        services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
        services.AddSingleton<ILanguageDetector, DotNetLanguageDetector>();
        services.AddSingleton<ILanguageDetector, TypeScriptLanguageDetector>();
        services.AddSingleton<ILanguageDetector, PythonLanguageDetector>();
        services.AddSingleton<IProjectDetector, ProjectDetector>();
        services.AddSingleton<IRepoSnapshotCollector, RepoSnapshotCollector>();
        services.AddSingleton<IContextValidator, ContextValidator>();
        services.AddSingleton<ContextUserPromptBuilder>();
        services.AddSingleton<IContextGenerator, ContextGenerator>();
        services.AddSingleton<ICodeMapGenerator, CodeMapGenerator>();
        services.AddSingleton<ICodingPrinciplesGenerator, CodingPrinciplesGenerator>();
        services.AddSingleton<ConceptVocabularyLoader>();
        services.AddSingleton<ConceptVocabularyValidator>();
        services.AddSingleton<SkillIndexBuilder>();
        services.AddSingleton<ISkillBodyResolver, SkillBodyResolver>();
        // p0111d: provider-override plumbing. Default registration uses a fresh
        // AgentSmithConfig (PrimaryProvider=null → no overrides). Composition roots
        // that want to honor an operator-set PrimaryProvider register a populated
        // AgentSmithConfig before this call to win the last-binding race.
        services.AddSingleton<AgentSmithConfig>(_ => new AgentSmithConfig());
        services.AddSingleton<IActiveProviderResolver, ActiveProviderResolver>();
        services.AddSingleton<IProviderOverrideResolver, ProviderOverrideResolver>();
        services.AddSingleton<ISkillLoader, YamlSkillLoader>();
        services.AddSingleton<IDecisionLogger, FileDecisionLogger>();

        services.AddHttpClient<ISkillsRepositoryClient, SkillsRepositoryClient>();
        services.AddSingleton<ISkillsCacheMarker, SkillsCacheMarker>();
        services.AddSingleton<SkillsCatalogPath>();
        services.AddSingleton<ISkillsCatalogPath>(sp => sp.GetRequiredService<SkillsCatalogPath>());
        services.AddSingleton<ISkillsSourceHandler, DefaultSourceHandler>();
        services.AddSingleton<ISkillsSourceHandler, PathSourceHandler>();
        services.AddSingleton<ISkillsSourceHandler, UrlSourceHandler>();
        services.AddSingleton<ISkillsCatalogResolver, SkillsCatalogResolver>();

        return services;
    }
}
