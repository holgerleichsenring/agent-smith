using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.DataMigrations;

/// <summary>
/// 2026-11-22-747da: applies the data migrations a store has not had, the way EF applies schema migrations — each
/// once, then recorded, never looked at again. A store created by this migrate holds no rows to change, so the caller
/// does not ask it; the ledger then arrives with an archive import, or the next migrate fills it over the new rows.
/// </summary>
public sealed class DataMigrationRunner(
    DataMigrationLedger ledger, SchemaWatermark watermark, ILogger<DataMigrationRunner> logger)
{
    /// <summary>Applies every migration not yet recorded, in order; returns how many ran.</summary>
    public async Task<int> RunAsync(
        AgentSmithDbContext db, IReadOnlyCollection<string> appliedBefore, IEnumerable<IDataMigration> migrations,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        var recorded = await ledger.RecordedAsync(db, ct);
        var ran = 0;
        foreach (var migration in migrations.Where(m => !recorded.Contains(m.Id)))
            ran += await ApplyAsync(db, appliedBefore, migration, ct) ? 1 : 0;
        return ran;
    }

    private async Task<bool> ApplyAsync(
        AgentSmithDbContext db, IReadOnlyCollection<string> appliedBefore, IDataMigration migration, CancellationToken ct)
    {
        if (watermark.Reached(appliedBefore, migration.ShippedWith))
        {
            await ledger.RecordAsync(db, migration.Id, proven: true, ct);
            logger.LogInformation("Data migration {Id}: already applied by an earlier migrate (schema at or after {Watermark}); recorded",
                migration.Id, migration.ShippedWith);
            return false;
        }
        // Each migration starts and ends on an empty tracker: one that failed mid-save must not hand its rows to the next.
        db.ChangeTracker.Clear();
        var complete = await RunClearingAsync(db, migration, ct);
        if (!complete)
        {
            logger.LogWarning("Data migration {Id} did not complete; the next migrate tries it again", migration.Id);
            return false;
        }
        await ledger.RecordAsync(db, migration.Id, proven: false, ct);
        logger.LogInformation("Data migration {Id}: applied and recorded", migration.Id);
        return true;
    }

    private static async Task<bool> RunClearingAsync(AgentSmithDbContext db, IDataMigration migration, CancellationToken ct)
    {
        try
        {
            return await migration.RunAsync(db, ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }
}
