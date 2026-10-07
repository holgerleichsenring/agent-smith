using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-06-03c7g: RunPhases becomes RunSpecs IN PLACE. A store on the previous schema,
/// holding a row, is migrated forward and the row is still there under the new names.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class RunSpecsRenameMigrationTests : IDisposable
{
    private const string MigrationBefore = "ReplaceApprovedSpecSetsWithApprovedSeries";
    private const string Rename = "RenameRunPhasesToRunSpecs";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public RunSpecsRenameMigrationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RenameRunPhasesToRunSpecs_StoredRow_IsKeptUnderTheNewNames()
    {
        await using (var db = Context())
        {
            await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO RunPhases (RunId, PhaseId, Ordinal, Title, Status, StartedAt, CreatedAt, UpdatedAt)
                VALUES ('run-1', 'p19213a', 1, 'Make it exist', 'done',
                        '2026-10-06 09:00:00+00:00', '2026-10-06 09:00:00+00:00', '2026-10-06 09:00:00+00:00');
                """);
            await db.Database.MigrateAsync();
        }

        await using var ctx = Context();
        var row = ctx.RunSpecs.Single();
        row.SpecId.Should().Be("p19213a");
        row.Title.Should().Be("Make it exist");
    }

    [Fact]
    public async Task RenameRunPhasesToRunSpecs_IsReversible()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync();
        await migrator.MigrateAsync(MigrationBefore);

        (await db.Database.GetAppliedMigrationsAsync())
            .Should().NotContain(id => id.EndsWith("_" + Rename, StringComparison.Ordinal));
        await db.Database.ExecuteSqlRawAsync("SELECT PhaseId FROM RunPhases");
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
