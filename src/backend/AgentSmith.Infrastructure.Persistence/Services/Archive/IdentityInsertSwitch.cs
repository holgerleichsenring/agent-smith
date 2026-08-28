using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: SQL Server refuses an insert that carries its own value for an
/// identity column unless identity insertion is switched on for that table — one table at
/// a time, on the same connection. So the import turns it on, writes the table, turns it
/// off. SQLite needs neither, which is why this belongs to the provider and not to the
/// archive format.
/// </summary>
public sealed class IdentityInsertSwitch(
    GeneratedKeyProperty generatedKey, ILogger<IdentityInsertSwitch> logger)
{
    private const string On = "ON";
    private const string Off = "OFF";

    /// <summary>True when the target provider would refuse this table's copied keys.</summary>
    public bool IsRequiredFor(AgentSmithDbContext db, IEntityType type)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Database.IsSqlServer() && generatedKey.Of(type) is not null;
    }

    /// <summary>Switches identity insertion on where the provider requires it; elsewhere,
    /// nothing — which is why the caller does not have to ask first.</summary>
    public Task EnableAsync(AgentSmithDbContext db, IEntityType type, CancellationToken ct) =>
        IsRequiredFor(db, type) ? SetAsync(db, type, On, ct) : Task.CompletedTask;

    public Task DisableAsync(AgentSmithDbContext db, IEntityType type, CancellationToken ct) =>
        IsRequiredFor(db, type) ? SetAsync(db, type, Off, ct) : Task.CompletedTask;

    private async Task SetAsync(
        AgentSmithDbContext db, IEntityType type, string state, CancellationToken ct)
    {
        // The identifier comes from the model, never from the archive.
        var table = db.GetService<ISqlGenerationHelper>()
            .DelimitIdentifier(type.GetTableName()!, type.GetSchema());
        // EF1002: the only interpolated value is the table name the MODEL declares.
        #pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} {state}", ct);
        #pragma warning restore EF1002
        logger.LogDebug("IDENTITY_INSERT {State} for {Table}.", state, table);
    }
}
