using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.DataMigrations;
using AgentSmith.Infrastructure.Persistence.Services.Repair;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-08-25-61f1: brings the run store up to date, repair first.
/// <para>
/// The order is the point. This spec's migration makes a run's recorded facts unique, and a
/// unique index cannot be created over rows that already violate it — the migration would
/// fail on the operator's database, at deploy, with the server unable to migrate itself. So
/// the rows the constraint would reject are removed first, on the schema the store is already
/// on, and only then does the migration run. Every caller that migrates goes through here, so
/// the ordering is a property of the code rather than of a runbook.
/// </para>
/// <para>
/// 2026-11-22-747da: the repair runs only while that migration is still pending — once the index exists no duplicate
/// can, and grouping every trail row on every deploy was a full scan for nothing. After the schema, the data
/// migrations the store has not had (the spec-name rewrite of 2026-10-12-967ab, the kept-set backfill of
/// 2026-11-22-a720a, and whatever the caller adds) run once each and are recorded.
/// </para>
/// </summary>
public sealed class RunStoreMigrator(
    RunDuplicateRepair repair, IEnumerable<IDataMigration> storeMigrations, DataMigrationRunner dataMigrations,
    SchemaWatermark watermark, ILogger<RunStoreMigrator> logger)
{
    /// <summary>The timestamp of AddRunRecordIdentity, the migration whose unique index the repair prepares.</summary>
    public const string UniqueTrailMigration = "20260825085711";

    public Task MigrateAsync(AgentSmithDbContext db, CancellationToken ct) => MigrateAsync(db, [], ct);

    /// <summary>Repair if needed, schema, then the store's data migrations followed by <paramref name="more"/>.</summary>
    public async Task MigrateAsync(AgentSmithDbContext db, IEnumerable<IDataMigration> more, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var before = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        if (!watermark.Reached(before, UniqueTrailMigration)) await RepairAsync(db, ct);
        await db.Database.MigrateAsync(ct);
        if (before.Count == 0)
        {
            logger.LogInformation("Data migrations: none applied to a store this migrate created, it holds no rows to change");
            return;
        }
        await dataMigrations.RunAsync(db, before, storeMigrations.Concat(more), ct);
    }

    public async Task<RunRepairReport> RepairAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        // A store with no applied migration holds no rows, so there is nothing to repair and
        // nothing to read from — the tables the repair would query do not exist yet.
        if (!(await db.Database.GetAppliedMigrationsAsync(ct)).Any()) return RunRepairReport.Nothing;
        var report = await repair.RepairAsync(db, ct);
        logger.LogInformation("Run store repair: {Repair}", report.Describe());
        return report;
    }
}
