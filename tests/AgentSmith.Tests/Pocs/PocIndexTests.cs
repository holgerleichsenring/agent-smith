using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933da: a live name is unique per target and a deleted PoC (LiveName null) frees it; an audit act is
/// recorded once per run, and entries no run wrote are not limited. Proven on SQLite always, and on SQL Server and
/// PostgreSQL when a server is handed to the suite; the SQL Server model's filters are checked offline.
/// </summary>
public sealed class PocIndexTests
{
    [Fact]
    public async Task Sqlite_PocAndAuditIndexes_RefuseDuplicatesAndAdmitNulls()
    {
        using var connection = MigratedStoreTemplate.OpenCopy();
        await ProveAsync(() => MigratedStoreTemplate.Context(connection));
    }

    [RequiresSqlServerFact]
    public async Task SqlServer_PocAndAuditIndexes_RefuseDuplicatesAndAdmitNulls()
    {
        await using var db = await ScratchSqlServer.MigratedAsync(RequiresSqlServerFactAttribute.ConnectionString!, "pocs");
        try { await ProveAsync(() => new AgentSmithDbContext(Options(db))); }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [RequiresPostgresFact]
    public async Task Postgres_PocAndAuditIndexes_RefuseDuplicatesAndAdmitNulls()
    {
        await using var db = await ScratchPostgres.MigratedAsync(RequiresPostgresFactAttribute.ConnectionString!, "pocs");
        try { await ProveAsync(() => new AgentSmithDbContext(Options(db))); }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public void SqlServerModel_UniqueIndexesOverNullableColumns_AreFilteredToNotNull()
    {
        using var db = SqlServerModelContext();

        Filter(db, typeof(Poc), nameof(Poc.LiveName)).Should().Be("[LiveName] IS NOT NULL");
        Filter(db, typeof(PocAuditEntry), nameof(PocAuditEntry.RunId)).Should().Be("[RunId] IS NOT NULL");
    }

    private static async Task ProveAsync(Func<AgentSmithDbContext> context)
    {
        await using (var db = context())
        {
            db.AddRange(PocRows.Poc("todo", "poc-ann"), PocRows.Poc("todo", "poc-ann", target: "second"),
                PocRows.Poc("gone", "poc-ann", deleted: true), PocRows.Poc("gone", "poc-ann", deleted: true));
            db.AddRange(PocRows.Entry(1, PocActs.Imported, "r-1"), PocRows.Entry(1, PocActs.Deployed, "r-1"),
                PocRows.Entry(1, PocActs.Extended, null), PocRows.Entry(1, PocActs.Extended, null));
            await db.SaveChangesAsync();
        }

        await RefusedAsync(context, db => db.Add(PocRows.Poc("todo", "poc-bob")), "a live name is unique per target");
        await RefusedAsync(context, db => db.Add(PocRows.Entry(2, PocActs.Imported, "r-1")), "an act is recorded once per run");
    }

    private static async Task RefusedAsync(Func<AgentSmithDbContext> context, Action<AgentSmithDbContext> add, string because)
    {
        await using var db = context();
        add(db);
        var write = async () => await db.SaveChangesAsync();
        await write.Should().ThrowAsync<DbUpdateException>(because);
    }

    private static string? Filter(DbContext db, Type entity, string column) =>
        db.Model.FindEntityType(entity)!.GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Any(p => p.Name == column)).GetFilter();

    private static DbContextOptions<AgentSmithDbContext> Options(AgentSmithDbContext db)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = db.Database.ProviderName!.Contains("Npgsql", StringComparison.Ordinal)
                ? PersistenceProvider.Postgresql : PersistenceProvider.SqlServer,
            ConnectionString = db.Database.GetConnectionString()!,
        });
        return builder.Options;
    }

    private static AgentSmithDbContext SqlServerModelContext()
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = PersistenceProvider.SqlServer,
            ConnectionString = "Server=localhost;Database=x;User Id=sa;Password=x;TrustServerCertificate=True",
        });
        return new AgentSmithDbContext(builder.Options);
    }
}
