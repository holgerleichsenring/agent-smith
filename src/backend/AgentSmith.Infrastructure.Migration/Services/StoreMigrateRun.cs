using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Migration.Services;

/// <summary>
/// 2026-11-22-747da: `database migrate` — the one body behind the CLI verb and the server image's migrate mode, run
/// before the server starts (an init container, a one-shot compose service), never by the server itself. Schema
/// first, then every data migration the store has not had, each once and recorded: a second migrate changes nothing.
/// </summary>
public sealed class StoreMigrateRun(
    IConfigStoreLocation location, BootstrapConfig bootstrap, IStartupFindings findings,
    MigrateContextFactory contexts, RunStoreMigrator migrator,
    RoleMappingMigration roles, PipelineAliasMigration aliases, LegacyTriggerMigration triggers,
    ProjectRegistriesMigration feeds, ReviewSettingMigration review,
    ILogger<StoreMigrateRun> logger)
{
    /// <summary>Migrates the store the bootstrap configuration names; 0 when it is current, 1 when it cannot be.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        if (Refusal() is { } refusal)
        {
            logger.LogError("{Refusal}", refusal);
            return 1;
        }
        try
        {
            await MigrateAsync(ct);
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 2026-11-22-747db: the server image's migrate mode has no command-line framework around it.
            logger.LogError(ex, "The database could not be migrated; the server must not start on it");
            return 1;
        }
    }

    private async Task MigrateAsync(CancellationToken ct)
    {
        var options = new PersistenceOptions
        {
            Provider = Enum.Parse<PersistenceProvider>(bootstrap.Persistence.Provider, ignoreCase: true),
            ConnectionString = bootstrap.Persistence.ConnectionString,
        };
        await using var db = await contexts.OpenAsync(options, ct);
        // Never the connection string — it may carry credentials.
        logger.LogInformation("Applying relational-persistence migrations (provider={Provider})...", options.Provider);
        await migrator.MigrateAsync(db, [roles, aliases, triggers, feeds, review], ct);
        logger.LogInformation("Done — schema and data are up to date.");
    }

    private string? Refusal()
    {
        if (!File.Exists(location.ConfigPath))
            return $"No configuration at '{location.ConfigPath}', so there is no database to migrate. Pass --config <path>.";
        if (findings.All.FirstOrDefault(f => f.Severity == StartupFindingSeverity.Blocking) is { } unreadable)
            return unreadable.Reason;
        return Enum.TryParse<PersistenceProvider>(bootstrap.Persistence.Provider, ignoreCase: true, out _)
            ? null
            : $"Unknown persistence.provider '{bootstrap.Persistence.Provider}' (expected sqlite | postgresql | mysql | sqlserver).";
    }
}
