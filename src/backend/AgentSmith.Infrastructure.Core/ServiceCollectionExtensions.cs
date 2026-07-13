using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
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
        services.AddSingleton<EffectiveTriggerBuilder>();
        services.AddSingleton<DeploymentDefaultsApplier>();
        // p0281a: connection repo discovery — the snapshot (hot cache) + durable disk
        // last-good + the sync glob expander the catalog resolver uses. The discovery
        // providers + refresher live in AgentSmith.Infrastructure (HTTP).
        services.AddSingleton<IConnectionRepoSnapshot, InMemoryConnectionRepoSnapshot>();
        services.AddSingleton<IConnectionRepoSnapshotStore, DiskConnectionRepoSnapshotStore>();
        services.AddSingleton<RepoGlobExpander>();
        // p0285: deterministic URL builder for exact (wildcard-free) connection repo refs.
        services.AddTransient<IConnectionRepoUrlBuilder, ConnectionRepoUrlBuilder>();
        services.AddSingleton<RepoCatalogBuilder>();
        services.AddSingleton<TrackerCatalogBuilder>();
        services.AddSingleton<ResolvedProjectBuilder>();
        services.AddSingleton<ConfigCatalogResolver>();
        services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
        services.AddSingleton<ConceptVocabularyLoader>();
        services.AddSingleton<ConceptVocabularyValidator>();
        services.AddSingleton<SkillIndexBuilder>();
        services.AddSingleton<ISkillBodyResolver, SkillBodyResolver>();
        // p0111d: provider-override plumbing. Default registration uses an empty
        // AgentSmithConfig (PrimaryProvider=null → no overrides). Composition roots
        // that want to honor an operator-set PrimaryProvider register a populated
        // AgentSmithConfig before this call to win the last-binding race.
        services.AddSingleton<AgentSmithConfig>(_ => AgentSmithConfig.Empty());
        services.AddSingleton<IActiveProviderResolver, ActiveProviderResolver>();
        services.AddSingleton<IProviderOverrideResolver, ProviderOverrideResolver>();
        services.AddSingleton<ISkillLoader, YamlSkillLoader>();
        services.AddSingleton<IDecisionLogger, FileDecisionLogger>();

        services.AddSingleton<IAgentSmithPaths, AgentSmithPaths>();
        // p0182: disk-backed ProjectMap cache is the CLI-safe default.
        // RedisExtensions (server-only) swaps in RedisProjectMapStore.
        services.AddSingleton<IProjectMapStore, DiskProjectMapStore>();

        services.AddHttpClient<ISkillsRepositoryClient, SkillsRepositoryClient>();
        services.AddSingleton<ISkillsCacheMarker, SkillsCacheMarker>();
        services.AddTransient<ICatalogTarballExtractor, CatalogTarballExtractor>();
        services.AddSingleton<IEmbeddedSkillsCatalog, EmbeddedSkillsCatalog>();
        services.AddSingleton<SkillsCatalogPath>();
        services.AddSingleton<ISkillsCatalogPath>(sp => sp.GetRequiredService<SkillsCatalogPath>());
        services.AddSingleton<ISkillsSourceHandler, DefaultSourceHandler>();
        services.AddSingleton<ISkillsSourceHandler, PathSourceHandler>();
        services.AddSingleton<ISkillsSourceHandler, UrlSourceHandler>();
        // p0325: the embedded catalog is the default resolution when no
        // explicit skills source is configured.
        services.AddSingleton<ISkillsSourceHandler, EmbeddedSourceHandler>();
        services.AddSingleton<ISkillsCatalogResolver, SkillsCatalogResolver>();

        return services;
    }
}
