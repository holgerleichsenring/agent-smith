using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Migration.Services;
using AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;
using AgentSmith.Infrastructure.Persistence.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Migration.Extensions;

/// <summary>
/// 2026-11-22-747da: the composition `database migrate` runs on — the same in the CLI and the server image. It reads
/// only the bootstrap slice of the configuration file (persistence and auth, environment overlays included).
/// </summary>
public static class StoreMigrateExtensions
{
    public static IServiceCollection AddStoreMigrate(this IServiceCollection services, string configPath)
    {
        services.AddSingleton<IConfigStoreLocation>(new FixedConfigStoreLocation(configPath));
        services.AddSingleton<IStartupFindings, StartupFindings>();
        services.AddSingleton<RawConfigYaml>().AddSingleton<AuthEnvironmentOverlay>().AddSingleton<PersistenceEnvironmentOverlay>();
        services.AddSingleton<BootstrapConfigReader>();
        services.AddSingleton(sp => sp.GetRequiredService<BootstrapConfigReader>().Read());
        services.AddSingleton(sp => sp.GetRequiredService<BootstrapConfig>().Auth ?? new TokenAuthorityConfig());
        services.AddSingleton<ConfigDocJson>().AddSingleton<ConfigDocumentAssembler>();
        services.AddSingleton<RoleMappingMigration>().AddSingleton<PipelineAliasMigration>().AddSingleton<LegacyTriggerMigration>();
        services.AddSingleton<ProjectRegistriesMigration>().AddSingleton<ReviewSettingMigration>(); // 2026-11-25-9992d, 9992i
        services.AddSingleton<MigrateContextFactory>().AddSingleton<StoreMigrateRun>();
        return services.AddRunStoreRepair();
    }
}
