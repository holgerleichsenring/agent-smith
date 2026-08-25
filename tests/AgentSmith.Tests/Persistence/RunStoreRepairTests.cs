using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Repair;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-08-25-61f1: the store cannot be constrained while it still holds the rows the
/// constraint forbids, so the repair and the constraint ship together and in that order.
/// Proven the way an operator meets it: a database on the previous schema, holding the
/// duplicates a replay left, migrated forward in one call.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class RunStoreRepairTests : IDisposable
{
    // The last migration before this phase's — the state a live database is in.
    private const string MigrationBeforeIdentity = "AddRunPhases";
    private const string RunId = "run-replayed";
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-08-24T09:00:00Z");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public RunStoreRepairTests() => _connection.Open();

    [Fact]
    public async Task Migration_OnAStoreHoldingDuplicates_RepairsBeforeItConstrains()
    {
        await SeedReplayedRunAsync(copies: 3);

        await using (var db = Context()) await Migrator().MigrateAsync(db, CancellationToken.None);

        await using var ctx = Context();
        ctx.RunEvents.Should().HaveCount(2, "one row per position survives, so the index can exist");
        ctx.RunLlmCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Migration_OnAStoreHoldingDuplicates_CannotBeAppliedWithoutTheRepair()
    {
        await SeedReplayedRunAsync(copies: 2);

        await using var db = Context();
        var migrate = async () => await db.Database.MigrateAsync();

        await migrate.Should().ThrowAsync<Exception>(
            "this is why the repair ships with the constraint and runs before it, rather than "
            + "failing on the operator's database at deploy");
    }

    [Fact]
    public async Task Repair_ASetOfDuplicates_KeepsTheEarliestAndRemovesTheRest()
    {
        await SeedReplayedRunAsync(copies: 3);

        await using (var db = Context()) await Migrator().RepairAsync(db, CancellationToken.None);

        await using var ctx = Context();
        ctx.RunEvents.Where(e => e.Seq == 0).Select(e => e.CreatedAt).Single()
            .Should().Be(T0, "the earliest copy is the one that reconstructs the run as it happened");
    }

    [Fact]
    public async Task Repair_ARunWhoseTotalWasSummedFromDuplicates_ReportsTheTrueTotalAfterwards()
    {
        await SeedReplayedRunAsync(copies: 3);

        await using (var db = Context()) await Migrator().RepairAsync(db, CancellationToken.None);

        await using var ctx = Context();
        ctx.Runs.Single().CostTotalUsd.Should().Be(2m, "three copies of one two-dollar call cost two dollars");
    }

    [Fact]
    public async Task Repair_ReportsWhatItRemovedAndWhichRunsMoved()
    {
        await SeedReplayedRunAsync(copies: 3);

        await using var db = Context();
        var report = await Migrator().RepairAsync(db, CancellationToken.None);

        report.RepairedRuns.Should().ContainSingle().Which.Should().Be(RunId);
        report.TrailRowsRemoved.Should().Be(4);
        report.LlmCallRowsRemoved.Should().Be(2);
        report.CostCorrections.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { RunId, Before = 6m, After = 2m });
        report.Describe().Should().Contain(RunId);
    }

    [Fact]
    public async Task Repair_AStoreWithNoDuplicates_ChangesNothingAndSaysSo()
    {
        await SeedReplayedRunAsync(copies: 1);

        await using var db = Context();
        var report = await Migrator().RepairAsync(db, CancellationToken.None);

        report.RowsRemoved.Should().Be(0);
        report.CostCorrections.Should().BeEmpty();
        report.Describe().Should().Contain("nothing to repair");
        report.RepairedRuns.Should().BeEmpty();
        Count("RunLlmCalls").Should().Be(1, "a run that was never replayed is left exactly as it is");
    }

    [Fact]
    public async Task Migration_AppliesAndIsReversible()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync();
        var forward = await db.Database.GetAppliedMigrationsAsync();
        await migrator.MigrateAsync(MigrationBeforeIdentity);
        var back = await db.Database.GetAppliedMigrationsAsync();

        forward.Should().Contain(id => id.EndsWith("_AddRunRecordIdentity", StringComparison.Ordinal));
        back.Should().NotContain(id => id.EndsWith("_AddRunRecordIdentity", StringComparison.Ordinal));
    }

    public void Dispose() => _connection.Dispose();

    private static RunStoreMigrator Migrator() =>
        new(new RunDuplicateRepair(new(), new(), new(), new()), NullLogger<RunStoreMigrator>.Instance);

    // Reads through the connection, not the model — the store may still be on the schema
    // that predates this phase, where the entity's new column does not exist yet.
    private long Count(string table)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    /// <summary>
    /// A run on the PREVIOUS schema whose two events, one per position, were projected
    /// <paramref name="copies"/> times — each pass stamping its own insert time, and each
    /// pass adding the call's cost to the run row the way the live accumulation did.
    /// </summary>
    private async Task SeedReplayedRunAsync(int copies)
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(MigrationBeforeIdentity);
        await db.Database.ExecuteSqlRawAsync(RunSql(copies * 2m));
        for (var pass = 0; pass < copies; pass++)
            await db.Database.ExecuteSqlRawAsync(PassSql(T0.AddMinutes(pass)));
    }

    private static string RunSql(decimal cost) =>
        $"""
         INSERT INTO Runs (Id, Project, Pipeline, TicketId, Status, StartedAt, CostTotalUsd,
                           TokensIn, TokensOut, CancelRequested, CreatedAt, UpdatedAt)
         VALUES ('{RunId}', 'p', 'fix-bug', 't', 'success', '{Stamp(T0)}', '{cost}',
                 0, 0, 0, '{Stamp(T0)}', '{Stamp(T0)}');
         """;

    private static string PassSql(DateTimeOffset at) =>
        $"""
         INSERT INTO RunEvents (RunId, Seq, Type, Timestamp, CreatedAt, UpdatedAt)
         VALUES ('{RunId}', 0, 'RunStarted', '{Stamp(T0)}', '{Stamp(at)}', '{Stamp(at)}'),
                ('{RunId}', 1, 'LlmCallFinished', '{Stamp(T0)}', '{Stamp(at)}', '{Stamp(at)}');
         INSERT INTO RunLlmCalls (RunId, Role, Phase, Model, TokensIn, TokensOut, CostUsd,
                                  DurationMs, CachedTokensIn, CacheCreationTokensIn, CreatedAt, UpdatedAt)
         VALUES ('{RunId}', 'coding-agent', 'plan', 'claude', 100, 20, '2', 500, 0, 0,
                 '{Stamp(at)}', '{Stamp(at)}');
         """;

    private static string Stamp(DateTimeOffset at) => at.ToString("yyyy-MM-dd HH:mm:sszzz");
}
