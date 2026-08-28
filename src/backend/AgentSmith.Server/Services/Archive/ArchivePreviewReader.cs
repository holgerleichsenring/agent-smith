using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: answers "what would I be downloading?" without producing anything —
/// the same table order and the same counts the manifest would carry, read straight from
/// the store.
/// </summary>
public sealed class ArchivePreviewReader(
    ArchiveTableOrder order, EntityTypeSet tables, MigrationHeadName head)
{
    public async Task<ArchivePreviewResponse> ReadAsync(
        AgentSmithDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var counted = new List<ArchivedTable>();
        foreach (var type in order.Of(db.Model))
            counted.Add(new ArchivedTable(
                type.GetTableName()!, await tables.CountAsync(db, type, cancellationToken)));

        return new ArchivePreviewResponse(
            head.Of(await db.Database.GetAppliedMigrationsAsync(cancellationToken)),
            db.Database.ProviderName ?? string.Empty,
            counted,
            counted.Sum(t => t.Rows));
    }
}
