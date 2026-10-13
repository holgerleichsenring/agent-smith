using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Conversation;

/// <summary>
/// 2026-10-13-9a59g: the conversation table arrives by one migration of one name in both sets, under the
/// CLR name (it has no DbSet), with its (RunId, EventSeq) identity filtered on SQL Server.
/// </summary>
public sealed class RunConversationEntryMigrationTests
{
    private const string Migration = "AddRunConversationEntries";

    [Fact]
    public async Task RunConversationEntry_Migrations_ApplyOnSqliteAndSqlServer()
    {
        using var connection = MigratedStoreTemplate.OpenCopy();
        await using (var sqlite = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options))
        {
            (await sqlite.Set<RunConversationEntry>().CountAsync()).Should().Be(0, "the migrated store carries the table");
            sqlite.Model.FindEntityType(typeof(RunConversationEntry))!.GetTableName().Should().Be("RunConversationEntry");
        }

        foreach (var provider in new[] { PersistenceProvider.Sqlite, PersistenceProvider.SqlServer })
            Named(provider).Should().ContainSingle().Which.Should().Be(Migration);

        using var sqlServer = new AgentSmithDbContext(Options(PersistenceProvider.SqlServer));
        var identity = sqlServer.Model.FindEntityType(typeof(RunConversationEntry))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["RunId", "EventSeq"]));
        (identity.IsUnique, identity.GetFilter()).Should().Be((true, "[EventSeq] IS NOT NULL"));
    }

    private static IReadOnlyList<string> Named(PersistenceProvider provider)
    {
        using var db = new AgentSmithDbContext(Options(provider));
        return [.. db.Database.GetMigrations().Select(id => id.Split('_', 2).Last()).Where(name => name == Migration)];
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
