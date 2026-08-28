using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: after a table's copied keys are written, the provider's generator has
/// to be past them — an import that leaves the database unable to take its next row has
/// not finished. On SQL Server the identity seed is reset to the largest key written; on
/// SQLite the rowid generator already takes the maximum plus one, so there is nothing to
/// do and saying so is the whole point of the guard.
/// </summary>
public sealed class IdentitySequenceAdvancer(
    GeneratedKeyProperty generatedKey, ILogger<IdentitySequenceAdvancer> logger)
{
    public async Task AdvanceAsync(
        AgentSmithDbContext db, IEntityType type, long maxKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (maxKey <= 0 || generatedKey.Of(type) is null || !db.Database.IsSqlServer()) return;

        var table = QualifiedName(type);
        // EF1002: the only interpolated value is the table name the MODEL declares.
        #pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"DBCC CHECKIDENT ('{table}', RESEED, {maxKey})", ct);
        #pragma warning restore EF1002
        logger.LogDebug("Advanced the identity generator of {Table} to {MaxKey}.", table, maxKey);
    }

    private static string QualifiedName(IEntityType type) =>
        type.GetSchema() is { } schema ? $"{schema}.{type.GetTableName()}" : type.GetTableName()!;
}
