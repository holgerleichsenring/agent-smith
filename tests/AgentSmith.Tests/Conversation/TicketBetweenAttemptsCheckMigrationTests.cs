using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Conversation;

/// <summary>
/// 2026-10-13-9a59h: the between-attempts check arrives by one migration of one name in both sets, one row per
/// last attempt (RunId unique).
/// </summary>
public sealed class TicketBetweenAttemptsCheckMigrationTests
{
    private const string Migration = "AddTicketBetweenAttemptsChecks";

    [Fact]
    public async Task TicketBetweenAttemptsCheck_Migrations_ApplyOnSqliteAndSqlServer()
    {
        using var connection = MigratedStoreTemplate.OpenCopy();
        await using (var sqlite = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options))
            (await sqlite.Set<TicketBetweenAttemptsCheck>().CountAsync()).Should().Be(0, "the migrated store carries the table");

        foreach (var provider in new[] { PersistenceProvider.Sqlite, PersistenceProvider.SqlServer })
        {
            using var db = new AgentSmithDbContext(Options(provider));
            db.Database.GetMigrations().Count(id => id.EndsWith("_" + Migration, StringComparison.Ordinal)).Should().Be(1);
            db.Model.FindEntityType(typeof(TicketBetweenAttemptsCheck))!.GetIndexes()
                .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["RunId"])).IsUnique.Should().BeTrue();
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
