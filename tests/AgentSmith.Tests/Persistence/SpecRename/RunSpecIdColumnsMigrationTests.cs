using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentSmith.Tests.Persistence.SpecRename;

/// <summary>
/// 2026-10-12-967ab: RunSteps.SpecId and RunDecisions.SpecId are added BESIDE PhaseId and filled
/// from it. 2026-10-12-967ah: PhaseId is dropped once every row's SpecId is filled from it; going
/// down restores it, filled from SpecId. The old column is read through SQL: no entity maps it.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class RunSpecIdColumnsMigrationTests : IDisposable
{
    private const string MigrationBefore = "AddTicketSeriesExecutedIds";
    private const string AddColumns = "AddRunSpecIdColumns";
    private const string DropColumns = "DropRunPhaseIdColumns";
    private const string Created = "'2026-10-12 09:00:00+00:00', '2026-10-12 09:00:00+00:00'";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public RunSpecIdColumnsMigrationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task AddRunSpecIdColumns_StoredRows_AreBackfilledFromTheOldColumn()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);
        await InsertOldRowsAsync(db, "run-1");

        await db.GetService<IMigrator>().MigrateAsync(AddColumns);

        (await ColumnAsync(db, "RunSteps", "SpecId")).Should().Equal("s1");
        (await ColumnAsync(db, "RunSteps", "PhaseId")).Should().Equal("s1");
        (await ColumnAsync(db, "RunDecisions", "SpecId")).Should().Equal("s1");
    }

    [Fact]
    public async Task Migration_DropPhaseIdColumns_UpDown()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);
        await InsertOldRowsAsync(db, "run-1");
        await migrator.MigrateAsync(AddColumns);
        await InsertOldRowsAsync(db, "run-2"); // an older replica wrote only the old column

        await migrator.MigrateAsync(DropColumns);

        (await ColumnAsync(db, "RunSteps", "SpecId")).Should().Equal("s1", "s1");
        (await ColumnAsync(db, "RunDecisions", "SpecId")).Should().Equal("s1", "s1");
        await FluentActions.Awaiting(() => ColumnAsync(db, "RunSteps", "PhaseId")).Should().ThrowAsync<SqliteException>();
        await FluentActions.Awaiting(() => ColumnAsync(db, "RunDecisions", "PhaseId")).Should().ThrowAsync<SqliteException>();

        await migrator.MigrateAsync(AddColumns);

        (await ColumnAsync(db, "RunSteps", "PhaseId")).Should().Equal("s1", "s1");
        (await ColumnAsync(db, "RunDecisions", "PhaseId")).Should().Equal("s1", "s1");
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

    private static Task InsertOldRowsAsync(AgentSmithDbContext db, string runId) =>
        db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO RunSteps (RunId, StepIndex, StepName, Status, PhaseId, LlmMs, ThrottleWaitMs, SandboxMs, CreatedAt, UpdatedAt)
            VALUES ('{runId}', 0, 'Verify', 'ok', 's1', 0, 0, 0, {Created});
            INSERT INTO RunDecisions (RunId, Name, PhaseId, CreatedAt, UpdatedAt)
            VALUES ('{runId}', 'a', 's1', {Created});
            """);

    private static async Task<List<string?>> ColumnAsync(AgentSmithDbContext db, string table, string column) =>
        await db.Database.SqlQueryRaw<string?>($"SELECT {column} AS Value FROM {table} ORDER BY Id").ToListAsync();

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
