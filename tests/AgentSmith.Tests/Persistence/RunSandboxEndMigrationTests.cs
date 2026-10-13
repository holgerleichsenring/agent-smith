using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-13-9a59i: RunSandboxes.Status is dropped for EndReason. The one recorded fact it held —
/// 'vanished' — is copied before the drop; 'failed', 'ok' and 'created' were never facts and read null.
/// The create's position is the row's identity, unique per run and filtered on SQL Server.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class RunSandboxEndMigrationTests : IDisposable
{
    private const string MigrationBefore = "AddTicketBetweenAttemptsChecks";
    private const string Migration = "RecordSandboxEndReason";
    private const string Created = "'2026-10-13 09:00:00+00:00', '2026-10-13 09:00:00+00:00'";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public RunSandboxEndMigrationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunSandboxMigration_CopiesVanishedStatus()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);
        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO RunSandboxes (RunId, Key, Status, CreatedAt, UpdatedAt) VALUES ('run-1', 'a', 'vanished', {Created});
            INSERT INTO RunSandboxes (RunId, Key, Status, CreatedAt, UpdatedAt) VALUES ('run-1', 'b', 'failed', {Created});
            INSERT INTO RunSandboxes (RunId, Key, Status, CreatedAt, UpdatedAt) VALUES ('run-1', 'c', 'created', {Created});
            """);

        await migrator.MigrateAsync(Migration);

        (await db.RunSandboxes.AsNoTracking().OrderBy(s => s.Id).Select(s => s.EndReason).ToListAsync())
            .Should().Equal("vanished", null, null);
        await FluentActions.Awaiting(() => db.Database.ExecuteSqlRawAsync("SELECT Status FROM RunSandboxes"))
            .Should().ThrowAsync<SqliteException>();

        await migrator.MigrateAsync(MigrationBefore);

        (await db.Database.SqlQueryRaw<string?>("SELECT Status AS Value FROM RunSandboxes ORDER BY Id").ToListAsync())
            .Should().Equal("vanished", null, null);
    }

    [Fact]
    public void RunSandbox_UniqueIndex_FilteredOnSqlServer()
    {
        foreach (var provider in new[] { PersistenceProvider.Sqlite, PersistenceProvider.SqlServer })
        {
            using var db = new AgentSmithDbContext(Options(provider));
            db.Database.GetMigrations().Select(id => id.Split('_', 2).Last()).Should().ContainSingle(n => n == Migration);
            var identity = db.Model.FindEntityType(typeof(RunSandbox))!.GetIndexes()
                .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["RunId", "EventSeq"]));
            (identity.IsUnique, identity.GetFilter()).Should().Be(
                (true, provider == PersistenceProvider.SqlServer ? "[EventSeq] IS NOT NULL" : null));
        }
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

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
