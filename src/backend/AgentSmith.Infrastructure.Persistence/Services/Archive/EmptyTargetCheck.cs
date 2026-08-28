using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: an archive is written into an EMPTY schema, and this is what says so.
/// Merging twenty-two tables into a store that already holds rows is a different problem
/// with different failure modes; offering it here would make the simple, needed case carry
/// the risk of the complicated one. A freshly migrated schema seeds no rows, so the
/// question is a cheap probe per table.
/// <para>
/// 2026-08-28-3793: this is the policy the CLI applies, and it stays the strict one. The
/// server states a different rule for its own database, which it has already written rows
/// into by the time anyone can ask for a restore.
/// </para>
/// </summary>
public sealed class EmptyTargetCheck(EntityTypeSet tables) : IImportTargetPolicy
{
    public async Task EnforceAsync(
        AgentSmithDbContext db, IReadOnlyList<IEntityType> types, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(types);
        foreach (var type in types)
        {
            if (!await tables.AnyAsync(db, type, cancellationToken)) continue;
            throw new DataArchiveException(
                $"Table '{type.GetTableName()}' already holds rows. An archive is written into an "
                + "empty database only — merging is not supported. Nothing was written.");
        }
    }
}
