using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;

namespace AgentSmith.Tests.Segments;

/// <summary>
/// 2026-10-13-9a59j: one segment per launch, opened by RunStarted at its trail position and closed by the
/// RunFinished above it. Replays with kept positions change nothing; the reconciler's position-less terminal
/// closes the open one; the rows go with the run. Proven through the real applier on SQLite.
/// </summary>
public sealed class RunSegmentProjectionTests : IDisposable
{
    private const string RunId = "2026-10-13T0900-seg";
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly RunEventApplier _applier = RunEventAppliers.Default();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunSegment_TwoParks_ThreeSegmentsWithTheirEnds()
    {
        await ApplyAsync((1, Started(0)), (2, Finished("waiting_for_input", 5)), (3, Started(10)),
            (4, Finished("waiting_for_input", 15)), (5, Started(20)), (6, Finished("success", 30)));

        (await SegmentsAsync()).Select(s => (s.EventSeq, s.StartedAt, s.EndSeq, s.EndedAt, s.EndStatus)).Should().Equal(
            (1, T, 2, T.AddMinutes(5), "waiting_for_input"),
            (3, T.AddMinutes(10), 4, T.AddMinutes(15), "waiting_for_input"),
            (5, T.AddMinutes(20), 6, T.AddMinutes(30), "success"));
    }

    [Fact]
    public async Task RunSegment_ReplayedRunStarted_AddsNoSegment()
    {
        await ApplyAsync((1, Started(0)), (2, Finished("waiting_for_input", 5)), (3, Started(10)));

        await ApplyAsync((1, Started(0)), (3, Started(10)));

        (await SegmentsAsync()).Select(s => s.EventSeq).Should().Equal(1L, 3L);
    }

    [Fact]
    public async Task RunSegment_ReplayedParkFinished_ChangesNothing()
    {
        var park = Finished("waiting_for_input", 5);
        await ApplyAsync((1, Started(0)), (2, park), (3, Started(10)));
        var before = await SegmentsAsync();

        await ApplyAsync((2, park));

        var after = await SegmentsAsync();
        after.Select(s => (s.EndSeq, s.EndedAt, s.EndStatus)).Should().Equal(before.Select(s => (s.EndSeq, s.EndedAt, s.EndStatus)));
        after[1].EndedAt.Should().BeNull("a replayed park never closes the later launch");
    }

    [Fact]
    public async Task RunSegment_PositionlessTerminal_ClosesOpenSegment()
    {
        await ApplyAsync((1, Started(0)), (2, Finished("waiting_for_input", 5)), (3, Started(10)));

        await ApplyAsync((null, Finished("waiting_for_input", 40)));
        (await SegmentsAsync())[1].EndedAt.Should().BeNull("only a terminal end arrives without a position");

        await ApplyAsync((null, Finished("failed", 41)));

        var segments = await SegmentsAsync();
        segments.Select(s => (s.EndSeq, s.EndStatus)).Should().Equal((2, "waiting_for_input"), (null, "failed"));
        segments[1].EndedAt.Should().Be(T.AddMinutes(41));
    }

    [Fact]
    public async Task RunSegment_DeletedWithRun()
    {
        await ApplyAsync((1, Started(0)), (2, Finished("waiting_for_input", 5)), (3, Started(10)));

        await using (var db = Context())
            await new RunDeletionRepository(db).DeleteAsync(RunId, CancellationToken.None);

        (await SegmentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RunSegment_UniqueIndex_FilteredOnSqlServer()
    {
        await using (var sqlite = Context())
        {
            sqlite.Model.FindEntityType(typeof(RunSegment))!.GetTableName().Should().Be("RunSegments");
            Identity(sqlite).Should().Be((true, null));
            sqlite.Add(new RunSegment { RunId = RunId, EventSeq = 7, StartedAt = T });
            sqlite.Add(new RunSegment { RunId = RunId, EventSeq = 7, StartedAt = T });
            await sqlite.Invoking(db => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        }

        foreach (var provider in new[] { PersistenceProvider.Sqlite, PersistenceProvider.SqlServer })
        {
            using var db = new AgentSmithDbContext(Options(provider));
            db.Database.GetMigrations().Count(id => id.EndsWith("_AddRunSegments")).Should().Be(1);
        }
        using var sqlServer = new AgentSmithDbContext(Options(PersistenceProvider.SqlServer));
        Identity(sqlServer).Should().Be((true, "[EventSeq] IS NOT NULL"));
    }

    private static (bool, string?) Identity(AgentSmithDbContext db)
    {
        var index = db.Model.FindEntityType(typeof(RunSegment))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["RunId", "EventSeq"]));
        return (index.IsUnique, index.GetFilter());
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

    private static RunStartedEvent Started(int minute) =>
        new(RunId, "ticket", "code", ["api"], T.AddMinutes(minute), "claude", "42");

    private static RunFinishedEvent Finished(string status, int minute) =>
        new(RunId, status, null, "s", T.AddMinutes(minute));

    private async Task ApplyAsync(params (long? Seq, RunEvent Event)[] events)
    {
        foreach (var (seq, ev) in events)
        {
            await using var db = Context();
            await _applier.ApplyAsync(db, ev, seq, CancellationToken.None);
        }
    }

    private async Task<List<RunSegment>> SegmentsAsync()
    {
        await using var db = Context();
        return await db.Set<RunSegment>().AsNoTracking().Where(s => s.RunId == RunId).OrderBy(s => s.EventSeq).ToListAsync();
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
