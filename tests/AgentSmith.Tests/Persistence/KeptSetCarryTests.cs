using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Repair;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Runway;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-11-22-a720a: a three-spec series, its first attempt delivering two specs and failing the third, then a Retry
/// unchanged that keeps the branch set (no revision) and delivers the third — projected into a migrated SQLite store and
/// read back the way the runway and the ticket page read it. Before the fix the kept attempt carried no ids, two specs
/// read as left and the runway started a fourth attempt.
/// </summary>
public sealed class KeptSetCarryTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 11, 22, 18, 0, 0, TimeSpan.Zero);
    private const string First = "2026-11-22T18-00-00-aaaa";
    private const string Retry = "2026-11-22T19-00-00-bbbb";
    private const string Series = "2026-11-22-297a";
    private static readonly string[] Ids = ["2026-11-22-297aa", "2026-11-22-297ab", "2026-11-22-297ac"];

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;
    private long _seq;

    public KeptSetCarryTests()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(new Mock<ITicketActivityNudge>().Object);
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task KeptRetry_DeliversTheLastSpec_SeriesReadsCompleteAndCarried()
    {
        await AttemptOneAsync();
        await ApplyAsync(Kept(Retry, carried: Ids[..2]));
        await ApplyAsync(State(Retry, 2, SpecRunState.Done));

        var records = await RecordsAsync();
        var left = TicketSeriesCuts.Left(records);

        left.Count.Should().Be(0, "the two carried specs and the one this attempt finished are the whole series");
        Judge(left).Should().Be(SeriesHeldReasons.Complete, "a complete series starts no further attempt");
        TicketSeriesCuts.StateOf(records, Retry, TicketSeriesCuts.Carried(records, Retry), Ids[0])
            .Should().Be(TicketSeriesCuts.CarriedState, "the page reads the carried specs as carried, not not_started");
    }

    [Fact]
    public async Task KeptPublish_RecordsSeriesAndCarriedIds_ButNoRevision()
    {
        await ApplyAsync(Kept(Retry, carried: Ids[..2]));

        var row = (await AttemptsAsync()).Single();
        row.SeriesId.Should().Be(Series);
        row.CarriedSpecIdsJson.Should().Be($"[\"{Ids[0]}\",\"{Ids[1]}\"]");
        row.RevisionNumber.Should().BeNull("a kept set wrote no revision of its own");
        row.ApprovalAt.Should().BeNull();
    }

    [Fact]
    public async Task LaterPublishInTheRun_AfterASpecFinished_KeepsTheCarriedIdsTheAttemptBeganWith()
    {
        await ApplyAsync(Kept(Retry, carried: Ids[..2]));
        await ApplyAsync(new SpecSetPublishedEvent(Retry, T.AddHours(2), 2, "spec review round 1", Series, Ids,
            Committed: true, SetKey: "github:42", Specs: Cut()));

        var row = (await AttemptsAsync()).Single();
        row.CarriedSpecIdsJson.Should().Be($"[\"{Ids[0]}\",\"{Ids[1]}\"]", "the third spec is this run's work, not carried");
        row.RevisionNumber.Should().Be(2, "the revision fields are the first revision publish's");
    }

    [Fact]
    public async Task Rewrite_RecordsTheLeftFingerprint_AndALaterPublishKeepsIt()
    {
        // 2026-11-22-e812
        await ApplyAsync(new TicketTextRewrittenEvent(Retry, "42", "Goal 1", new string('b', 64), T));
        await ApplyAsync(Kept(Retry, carried: Ids[..2]));

        (await AttemptsAsync()).Single().LeftTicketFingerprint.Should().Be(new string('b', 64));
    }

    [Fact]
    public async Task Backfill_RowWithoutCarriedIds_IsFilledFromItsKeptPublish_AndASecondPassChangesNothing()
    {
        await StoreAsync(Kept(Retry, carried: Ids[..2]), carriedJson: null);
        await StoreAsync(new SpecSetPublishedEvent(First, T, 1, "initial", Series, [], Committed: true,
            SetKey: "github:42", Specs: Cut()), carriedJson: "[]");
        await StoreAsync(new SpecSetPublishedEvent("2026-11-22T20-00-00-cccc", T, SetKey: "github:42", HandbackCase: "missing", Specs: []),
            carriedJson: null);

        var backfill = new KeptSetCarryBackfill(NullLogger<KeptSetCarryBackfill>.Instance);
        await using var db = new AgentSmithDbContext(Options());
        var first = await backfill.RepairAsync(db, CancellationToken.None);
        var second = await backfill.RepairAsync(db, CancellationToken.None);

        var rows = (await AttemptsAsync()).ToDictionary(a => a.RunId);
        first.Should().Be(1);
        second.Should().Be(0);
        rows[Retry].CarriedSpecIdsJson.Should().Be($"[\"{Ids[0]}\",\"{Ids[1]}\"]");
        rows[Retry].SeriesId.Should().Be(Series);
        rows[First].CarriedSpecIdsJson.Should().Be("[]", "a filled row is left as it is");
        rows["2026-11-22T20-00-00-cccc"].CarriedSpecIdsJson.Should().Be("[]", "a row whose events carry none reads as before");
    }

    private async Task AttemptOneAsync()
    {
        await ApplyAsync(new SpecSetPublishedEvent(First, T, 1, "initial", Series, [], Committed: true,
            SetKey: "github:42", Specs: Cut()));
        await ApplyAsync(State(First, 0, SpecRunState.Done));
        await ApplyAsync(State(First, 1, SpecRunState.Done));
        await ApplyAsync(State(First, 2, SpecRunState.Failed));
    }

    private static SpecSetPublishedEvent Kept(string runId, string[] carried) =>
        new(runId, T.AddHours(1), null, null, Series, carried, Committed: false, SetKey: "github:42", Specs: Cut());

    private static List<SpecCutEntry> Cut() =>
        [.. Ids.Select((id, i) => new SpecCutEntry(id, i + 1, id, $"Goal {i + 1}", ["api"]))];

    private static SpecStateChangedEvent State(string runId, int index, SpecRunState state) =>
        new(runId, Ids[index], index + 1, $"Goal {index + 1}", state, null, T.AddMinutes(10 * (index + 1)));

    private static string Judge(SeriesLeft left)
    {
        var run = new Run { Id = Retry, Pipeline = "code", Status = RunStatuses.Success, StartedAt = T, FinishedAt = T.AddHours(1) };
        var facts = new SeriesRunwayFacts([run], new Dictionary<string, string?> { [Retry] = AttemptChannels.Retrigger }, run,
            Queued: false, HasCut: left.Cut is not null, SpecsLeft: left.Count, NewestDoneSpecs: true, PendingHoldingSeries: 0,
            Watermark: null, RefusedMark: false, ClosedMark: false, SeriesSettingsDoubles.Settings(null));
        return SeriesRunway.Judge(facts, T.AddHours(2)).Next;
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(Contracts.Events.RunEvent ev)
    {
        using var scope = _provider.CreateScope();
        await _provider.GetRequiredService<RunEventApplier>().ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, ++_seq, CancellationToken.None);
    }

    // The store as a run before the fix left it: the event on the trail, the attempt row without the ids.
    private async Task StoreAsync(Contracts.Events.RunEvent ev, string? carriedJson)
    {
        await using var db = new AgentSmithDbContext(Options());
        db.RunEvents.Add(RunTrailRowMapper.Map(ev.RunId, ++_seq, ev));
        db.Add(new RunAttempt { RunId = ev.RunId, CarriedSpecIdsJson = carriedJson });
        await db.SaveChangesAsync();
    }

    private async Task<List<RunAttempt>> AttemptsAsync()
    {
        await using var db = new AgentSmithDbContext(Options());
        return await db.Set<RunAttempt>().AsNoTracking().ToListAsync();
    }

    private Task<TicketSeriesRecords> RecordsAsync() =>
        new TicketSeriesRecordsReader(_provider.GetRequiredService<IServiceScopeFactory>())
            .ReadAsync([First, Retry], full: false, CancellationToken.None);
}
