using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.Repair;
using AgentSmith.Infrastructure.Persistence.Services.SpecRename;
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
/// 2026-10-12-967ab: and the stored-name rewrite last, because it reads and writes the columns
/// the migration adds — rows written in the pre-rename names are rewritten in the spec names.
/// </para>
/// </summary>
public sealed class RunStoreMigrator(
    RunDuplicateRepair repair, StoredSpecNameRewrite specNames, ILogger<RunStoreMigrator> logger)
{
    public async Task MigrateAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        await RepairAsync(db, ct);
        await db.Database.MigrateAsync(ct);
        await specNames.RewriteAsync(db, ct);
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
