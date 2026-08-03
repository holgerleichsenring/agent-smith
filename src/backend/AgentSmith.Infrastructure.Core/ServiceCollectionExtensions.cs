using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Core.Services.Demo;
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
        // p0391a: the findings list every startup dependency and configuration rule
        // records into instead of throwing. Singleton — it IS the installation's
        // current "what is wrong" picture, read by the endpoint and the dashboard.
        services.AddSingleton<IStartupFindings, StartupFindings>();
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
        // p0349: the shared raw->typed pipeline both the file loader and the
        // server's DB loader run over a RawAgentSmithConfig.
        services.AddSingleton<RawConfigMaterializer>();
        services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
        // p0345/p0349: config studio — the catalog behind IConfigStore. The
        // read-only file store keeps the CLI/pipelines running purely from
        // agentsmith.yml with zero DB; the server swaps in DbConfigStore.
        services.AddSingleton<IConfigStoreLocation, EnvConfigStoreLocation>();
        services.AddSingleton<IConfigStore, FileConfigStore>();
        // p0349: the type<->model assembly map, shared by DbConfigStore + the
        // server's DB configuration loader; and the bootstrap reader that pulls
        // persistence + secret names from the file before the DB is reachable.
        // p0392: what the server would say about an unsaved studio draft, from the
        // server's own rule objects.
        services.AddSingleton<Services.Configuration.Studio.ConfigDraftRules>();
        services.AddSingleton<ConfigDocumentAssembler>();
        services.AddSingleton<BootstrapConfigReader>();
        services.AddSingleton<ConceptVocabularyLoader>();
        services.AddSingleton<ConceptVocabularyValidator>();
        // p0313b: the body resolver inlines a master's {{ref:<slug>}} citations from
        // the catalog's references/ directory, so the shared methodology has one home.
        services.AddSingleton<ISkillReferenceSource, CatalogSkillReferenceSource>();
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
        // p0326: the demo's bundled sample project rides the same embedded-tarball shape.
        services.AddSingleton<IEmbeddedDemoSample, EmbeddedDemoSample>();
        services.AddSingleton<SkillsCatalogPath>();
        services.AddSingleton<ISkillsCatalogPath>(sp => sp.GetRequiredService<SkillsCatalogPath>());
        // p0379: authored principles core+delta composition from the resolved catalog.
        services.AddSingleton<IPrinciplesTemplateSource, CatalogPrinciplesTemplateSource>();
        services.AddSingleton<ISkillsSourceHandler, DefaultSourceHandler>();
        services.AddSingleton<ISkillsSourceHandler, PathSourceHandler>();
        services.AddSingleton<ISkillsSourceHandler, UrlSourceHandler>();
        // p0325: the embedded catalog is the default resolution when no
        // explicit skills source is configured.
        services.AddSingleton<ISkillsSourceHandler, EmbeddedSourceHandler>();
        services.AddSingleton<ISkillsCatalogResolver, SkillsCatalogResolver>();
        // p0358: eager, logged catalog refresh when a config reload changes skills.version.
        services.AddSingleton<SkillsCatalogRefresher>();

        return services;
    }
}
