using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentSmith.Tests.Persistence.SpecRename;

/// <summary>
/// 2026-10-12-967ab: RunSteps.SpecId and RunDecisions.SpecId are added BESIDE PhaseId (a replica
/// still on 967aa keeps querying PhaseId) and filled from it; going down drops only the new column.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class RunSpecIdColumnsMigrationTests : IDisposable
{
    private const string MigrationBefore = "AddTicketSeriesExecutedIds";
    private const string AddColumns = "AddRunSpecIdColumns";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public RunSpecIdColumnsMigrationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task AddRunSpecIdColumns_StoredRows_AreBackfilledFromTheOldColumn()
    {
        await using (var db = Context())
        {
            await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO RunSteps (RunId, StepIndex, StepName, Status, PhaseId, LlmMs, ThrottleWaitMs, SandboxMs, CreatedAt, UpdatedAt)
                VALUES ('run-1', 0, 'Verify', 'ok', 's1', 0, 0, 0, '2026-10-12 09:00:00+00:00', '2026-10-12 09:00:00+00:00');
                INSERT INTO RunDecisions (RunId, Name, PhaseId, CreatedAt, UpdatedAt)
                VALUES ('run-1', 'a', 's1', '2026-10-12 09:00:00+00:00', '2026-10-12 09:00:00+00:00');
                """);
            await db.Database.MigrateAsync();
        }

        await using var ctx = Context();
        ctx.RunSteps.Single().Should().Match<AgentSmith.Infrastructure.Persistence.Entities.RunStep>(
            s => s.SpecId == "s1" && s.PhaseId == "s1");
        ctx.RunDecisions.Single().SpecId.Should().Be("s1");
    }

    [Fact]
    public async Task AddRunSpecIdColumns_IsReversible()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync();
        await migrator.MigrateAsync(MigrationBefore);

        (await db.Database.GetAppliedMigrationsAsync())
            .Should().NotContain(id => id.EndsWith("_" + AddColumns, StringComparison.Ordinal));
        await db.Database.ExecuteSqlRawAsync("SELECT PhaseId FROM RunSteps; SELECT PhaseId FROM RunDecisions");
        var act = () => db.Database.ExecuteSqlRawAsync("SELECT SpecId FROM RunSteps");
        await act.Should().ThrowAsync<SqliteException>();
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
