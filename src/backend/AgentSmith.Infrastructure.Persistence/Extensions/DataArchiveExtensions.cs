using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// 2026-08-28-2af6: the data archive's graph. It lives in the persistence project rather
/// than in the CLI, because the CLI is not the only caller a store copy has: the server
/// reaches for the same writer and reader over its own connection.
/// </summary>
public static class DataArchiveExtensions
{
    public static IServiceCollection AddDataArchive(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ArchiveRowCodec>();
        services.AddSingleton<ArchiveTableOrder>();
        services.AddSingleton<EntityTypeSet>();
        services.AddSingleton<MigrationHeadName>();
        services.AddSingleton<GeneratedKeyProperty>();
        services.AddSingleton<ArchiveSchemaCheck>();
        // 2026-08-28-3793: the CLI's policy is the default, and the server replaces this
        // one registration with its own rule.
        services.AddSingleton<IImportTargetPolicy, EmptyTargetCheck>();
        services.AddSingleton<ArchiveTableImporter>();
        services.AddSingleton<IdentityInsertSwitch>();
        services.AddSingleton<IdentitySequenceAdvancer>();
        services.AddSingleton<ImportedRowCountVerifier>();
        services.AddSingleton<IDataArchiveWriter, DataArchiveWriter>();
        services.AddSingleton<IDataArchiveReader, DataArchiveReader>();
        return services;
    }
}
