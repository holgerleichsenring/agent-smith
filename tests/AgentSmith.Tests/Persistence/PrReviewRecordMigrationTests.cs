using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-13-9a59b: the pull-request record tables arrive through one migration of one name in
/// both migration sets, with their unique keys.
/// </summary>
public sealed class PrReviewRecordMigrationTests
{
    private const string Migration = "AddPrReviewRecords";

    [Fact]
    public async Task PrReviewRecord_Migrations_ApplyOnSqliteAndSqlServer()
    {
        using var connection = MigratedStoreTemplate.OpenCopy();
        await using (var db = MigratedStoreTemplate.Context(connection))
        {
            db.Add(new PrReviewEntry { PrKey = "k", HostId = "h", Kind = "comment" });
            await db.SaveChangesAsync();
        }
        await using (var db = MigratedStoreTemplate.Context(connection))
        {
            db.Add(new PrReviewEntry { PrKey = "k", HostId = "h", Kind = "comment" });
            var duplicate = () => db.SaveChangesAsync();
            await duplicate.Should().ThrowAsync<DbUpdateException>("one entry per host id of a pull request");
        }

        using var sqlServer = new AgentSmithDbContext(Options(PersistenceProvider.SqlServer));
        var id = sqlServer.Database.GetMigrations().Single(m => m.EndsWith("_" + Migration, StringComparison.Ordinal));
        var previous = sqlServer.Database.GetMigrations().TakeWhile(m => m != id).Last();
        var script = sqlServer.GetService<IMigrator>().GenerateScript(previous, id);
        script.Should().Contain("CREATE TABLE [PrReviewRecords]").And.Contain("CREATE TABLE [PrReviewEntries]")
            .And.Contain("CREATE UNIQUE INDEX [IX_PrReviewRecords_PrKey]")
            .And.Contain("CREATE UNIQUE INDEX [IX_PrReviewEntries_PrKey_HostId]");
        new AgentSmithDbContext(Options(PersistenceProvider.Sqlite)).Database.GetMigrations()
            .Should().ContainSingle(m => m.EndsWith("_" + Migration, StringComparison.Ordinal));
    }

    [RequiresSqlServerFact]
    public async Task PrReviewRecord_SqlServerMigrated_StoresARecord()
    {
        await using var db = await ScratchSqlServer.MigratedAsync(RequiresSqlServerFactAttribute.ConnectionString!, "prreview");
        try
        {
            db.Add(new PrReviewRecord { PrKey = new string('a', 64), Url = "u", Project = "p", TicketId = "7", RunId = "r", State = "open" });
            await db.SaveChangesAsync();
            (await db.Set<PrReviewRecord>().CountAsync()).Should().Be(1);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static DbContextOptions<AgentSmithDbContext> Options(PersistenceProvider provider)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = provider,
            ConnectionString = provider == PersistenceProvider.Sqlite
                ? "Data Source=:memory:"
                : "Server=localhost;Database=x;TrustServerCertificate=True",
        });
        return builder.Options;
    }
}
