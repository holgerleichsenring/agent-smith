using AgentSmith.Domain.Exceptions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: an archive is written into an EMPTY schema, and this is what says so.
/// Merging twenty-two tables into a store that already holds rows is a different problem
/// with different failure modes; offering it here would make the simple, needed case carry
/// the risk of the complicated one. A freshly migrated schema seeds no rows, so the
/// question is a cheap probe per table.
/// </summary>
public sealed class EmptyTargetCheck(EntityTypeSet tables)
{
    public async Task VerifyAsync(
        AgentSmithDbContext db, IReadOnlyList<IEntityType> types, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(types);
        foreach (var type in types)
        {
            if (!await tables.AnyAsync(db, type, ct)) continue;
            throw new DataArchiveException(
                $"Table '{type.GetTableName()}' already holds rows. An archive is written into an "
                + "empty database only — merging is not supported. Nothing was written.");
        }
    }
}
