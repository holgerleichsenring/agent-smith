using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.DataMigrations;

/// <summary>
/// 2026-11-22-747da: reads and writes DataMigrationHistory. Two replicas' init containers can finish the same data
/// migration at once; the second insert then meets the first's key, and that row already says what this one would.
/// </summary>
public sealed class DataMigrationLedger(ILogger<DataMigrationLedger> logger, TimeProvider? clock = null)
{
    public async Task<IReadOnlySet<string>> RecordedAsync(AgentSmithDbContext db, CancellationToken ct) =>
        (await db.Set<DataMigrationRecord>().AsNoTracking().Select(r => r.Id).ToListAsync(ct))
        .ToHashSet(StringComparer.Ordinal);

    public async Task RecordAsync(AgentSmithDbContext db, string id, bool proven, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        db.ChangeTracker.Clear();
        db.Set<DataMigrationRecord>().Add(new DataMigrationRecord
        {
            Id = id, Proven = proven, AppliedAt = (clock ?? TimeProvider.System).GetUtcNow(),
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (!await ExistsAsync(db, id, ct))
            {
                logger.LogError(ex, "Data migration {Id} could not be recorded", id);
                throw;
            }
            logger.LogInformation(ex, "Data migration {Id} was recorded by another migrate at the same time", id);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private static async Task<bool> ExistsAsync(AgentSmithDbContext db, string id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        return await db.Set<DataMigrationRecord>().AsNoTracking().AnyAsync(r => r.Id == id, ct);
    }
}
