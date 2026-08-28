using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: proves the copy instead of assuming it. After the tables are written
/// and before the transaction commits, every table's row count is read back and compared
/// to what the manifest promised; a difference names the tables that disagree and takes
/// the whole import down with it.
/// </summary>
public sealed class ImportedRowCountVerifier(EntityTypeSet tables)
{
    public async Task VerifyAsync(
        AgentSmithDbContext db, DataArchiveManifest manifest,
        IReadOnlyList<IEntityType> types, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(manifest);
        var promised = manifest.Tables.ToDictionary(t => t.Table, t => t.Rows, StringComparer.Ordinal);
        var disagreeing = new List<string>();
        foreach (var type in types)
        {
            var table = type.GetTableName()!;
            var found = Disagreement(table, promised, await tables.CountAsync(db, type, ct));
            if (found is not null) disagreeing.Add(found);
        }

        if (disagreeing.Count == 0) return;
        throw new DataArchiveException(
            "The import did not write what the manifest promised — "
            + string.Join("; ", disagreeing) + ". Nothing was committed.");
    }

    private static string? Disagreement(
        string table, IReadOnlyDictionary<string, long> promised, long actual)
    {
        if (!promised.TryGetValue(table, out var expected))
            return $"{table}: not named in the manifest, {actual} rows present";
        return expected == actual ? null : $"{table}: manifest {expected}, database {actual}";
    }
}
