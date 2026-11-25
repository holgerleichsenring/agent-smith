using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Holds;
using AgentSmith.Server.Services.Tickets.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-31dcg: the holds as the ticket list, the ticket page and the series page read them over a real
/// migrated store — a series' next merge is Your move and the one after it nobody's, an admin hold is stuck and listed in
/// Your move only when asked for, an edited text holds until the conversation answers it, and the merge order is read once
/// per project however many tickets the list holds.</summary>
public sealed class TicketHoldListTests : IDisposable
{
    private readonly SeriesRunRepositoryCounter _series = new();
    private readonly TicketStoreHarness _h;

    public TicketHoldListTests()
    {
        _h = new TicketStoreHarness(services =>
        {
            services.AddSingleton<ISeriesRunStore>(sp => _series.Bind(new SeriesRunRepository(sp.GetRequiredService<IServiceScopeFactory>())));
            services.AddScoped<SeriesRunNames>().AddScoped<SeriesRunCosts>().AddScoped<SeriesRunDetailReader>().AddScoped<SeriesParkedQuestion>().AddScoped<SeriesStatusReader>() /* 2026-11-25-9992a */;
            services.AddTransient<SeriesRunHolds>();
        });
        _h.Config.Projects["alpha"] = new()
        {
            Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            Repos = [new RepoConnection { Name = "api", DefaultBranch = "main" }],
        };
    }

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task List_TheNextMergeOfASeries_IsYourMove_TheOneAfterIt_IsNobodys()
    {
        await SeriesAsync("sr-1", "1", "2");
        await _h.AddAsync(Run("r-1", "1"), Step("r-1"), Run("r-2", "2"), Step("r-2"), Record("1"), Record("2"), Requires("2", "1"));

        var list = await ListAsync();

        var next = list.Tickets.Single(t => t.TicketId == "1");
        (next.Bucket, next.Hold!.Case, next.Hold.Act).Should().Be((TicketBucket.NeedsYou, TicketHoldCases.MergeNext,
            new TicketHoldAct(TicketHoldActs.Merge, "sr-1")));
        var after = list.Tickets.Single(t => t.TicketId == "2");
        (after.Bucket, after.Hold!.Addressee, after.Hold.Case).Should().Be(
            (TicketBucket.Working, TicketHoldAddressees.Nobody, TicketHoldCases.MergeAfter));
        after.Hold.Names.Should().Equal("SCRUM-1");
        list.Counts.NeedsYou.Should().Be(1, "only the merge that comes next is Your move");
        (next.Hold.Next, next.Hold.Approves).Should().Be(("SCRUM-2", false), "2026-11-23-849e: the step after it; alpha bypasses protection");
    }

    // 2026-11-23-849e: the series page, its list row and the ticket's hold share one rule — a ready step whose CI still runs
    // is no merge for a person, and the next merge is named only while the hold is that merge.
    [Fact]
    public async Task SeriesPage_AReadyStepWithCiRunning_IsNotTheNextMerge()
    {
        await SeriesAsync("sr-1", "1", "2");
        await _h.AddAsync(Run("r-1", "1"), Step("r-1"), Record("1", CiStates.Pending), Run("r-2", "2"), Step("r-2"), Record("2"),
            Requires("2", "1"));
        await using var scope = _h.Services.CreateAsyncScope();

        var series = (await scope.ServiceProvider.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default))!;

        series.MergeOrder.First().Wait.Should().Be(AgentSmith.Application.Services.Series.SeriesRunMergeWaits.Ready);
        series.Tickets.Single(t => t.TicketId == "1").Hold!.Case.Should().Be(TicketHoldCases.CiRunning);
        var holds = series.Tickets.Where(t => t.Hold is not null).ToDictionary(t => t.TicketId, t => t.Hold!);
        SeriesRunTickets.NextMerge(series.MergeOrder, holds).Should().BeNull();
        SeriesRunTickets.NextMerge(series.MergeOrder, null).Should().Be("SCRUM-1", "without holds the merge order alone decides");
    }

    [Fact]
    public async Task List_APullRequestOutsideASeries_MergesNowOrWaitsWithItsReason_RedCiIsADecision()
    {
        var running = Record("3", CiStates.Pending);
        running.CiAtTicks = T.AddMinutes(170).UtcTicks; // 2026-11-23-849e: ten minutes before the clock, short of the escalation
        await _h.AddAsync(Run("r-1", "1"), Step("r-1"), Record("1", CiStates.Green),
            Run("r-2", "2"), Step("r-2"), Record("2", CiStates.Green), Requires("2", "1"),
            Run("r-3", "3"), Step("r-3"), running,
            Run("r-4", "4"), Step("r-4"), Record("4", CiStates.Red));

        var list = await ListAsync();
        var row = list.Tickets.ToDictionary(t => t.TicketId);

        (row["1"].Bucket, row["1"].Hold!.Case, row["1"].Hold!.Act).Should().Be((TicketBucket.NeedsYou, TicketHoldCases.MergeReady,
            new TicketHoldAct(TicketHoldActs.PullRequest, "https://github.com/o/api/pull/1")));
        (row["2"].Bucket, row["2"].Hold!.Case).Should().Be((TicketBucket.Working, TicketHoldCases.MergeAfter));
        row["2"].Hold!.Names.Should().Equal("#1");
        (row["3"].Bucket, row["3"].Hold!.Case, row["3"].Hold!.Since).Should().Be((TicketBucket.Working, TicketHoldCases.CiRunning, (DateTimeOffset?)T.AddMinutes(170)));
        (row["4"].Bucket, row["4"].Hold!.Addressee, row["4"].Hold!.Case).Should().Be((TicketBucket.NeedsYou, TicketHoldAddressees.Decide, TicketHoldCases.CiFailed));
        row["4"].Hold!.Names.Should().Equal("build");
        (list.Counts.NeedsYou, list.Counts.Working, list.Counts.Running, list.Counts.Queued).Should().Be((2, 2, 0, 0),
            "every ticket is in one bucket, and a pull request that waits is neither running nor queued");
        var page = (await _h.Detail.ReadAsync("alpha", "2", default))!.Summary;
        page.Hold.Should().BeEquivalentTo(row["2"].Hold, "the ticket page names the hold the list names");
    }

    [Fact]
    public async Task List_AnAdminHold_IsStuck_ListedInYourMoveOnlyWhenAsked_TheReadTheSameForBoth()
    {
        var run = Run("r-1", "42", status: RunStatuses.Failed);
        run.EndKind = RunEndKinds.ImageUntrusted;
        await _h.AddAsync(run, Step("r-1"));

        var plain = await ListAsync();
        var mine = await _h.List.ReadAsync(new TicketListRequest(ProjectScope.Single("alpha"), TicketBucket.NeedsYou, null, null), default);
        var admin = await _h.List.ReadAsync(
            new TicketListRequest(ProjectScope.Single("alpha"), TicketBucket.NeedsYou, null, null) { AdminHolds = true }, default);

        var row = plain.Tickets.Single();
        (row.Bucket, row.Hold!.Addressee).Should().Be((TicketBucket.Stuck, TicketHoldAddressees.Admin));
        (plain.Counts.NeedsYou, plain.Counts.AdminHolds, plain.Counts.Stuck).Should().Be((0, 1, 1));
        mine.Tickets.Should().BeEmpty("a person who is no admin is not shown the admin's hold");
        admin.Tickets.Should().ContainSingle().Which.TicketId.Should().Be("42");
        admin.Counts.Should().BeEquivalentTo(mine.Counts, "the counts are read the same for every viewer");
    }

    [Fact]
    public async Task List_AnEditedText_IsAPlanHold_UntilTheConversationAnswersIt()
    {
        await _h.AddAsync(Run("r-1", "42"), Step("r-1"), Record("42"));
        await LedgerAsync(l => l.TryMarkSpokenAsync("alpha", "42", TicketTextEditNotice.KeyPrefix + "fp-1", default));

        var held = (await ListAsync()).Tickets.Single();
        (held.Bucket, held.Hold!.Addressee).Should().Be((TicketBucket.NeedsYou, TicketHoldAddressees.Plan));

        await _h.Services.GetRequiredService<TicketTextEditAnswers>().AnsweredAsync("alpha", "gh", "github-42", default);

        var answered = (await ListAsync()).Tickets.Single();
        (answered.Bucket, answered.Hold!.Case).Should().Be((TicketBucket.NeedsYou, TicketHoldCases.MergeReady));
    }

    [Fact]
    public async Task Answered_AnotherTicketsConversation_LeavesTheEditOpen()
    {
        await _h.AddAsync(Run("r-1", "42"), Step("r-1"), Record("42"));
        await LedgerAsync(l => l.TryMarkSpokenAsync("alpha", "42", TicketTextEditNotice.KeyPrefix + "fp-1", default));

        await _h.Services.GetRequiredService<TicketTextEditAnswers>().AnsweredAsync("alpha", "gh", "github-43", default);
        await _h.Services.GetRequiredService<TicketTextEditAnswers>().AnsweredAsync("alpha", "other", "github-42", default);

        (await ListAsync()).Tickets.Single().Hold!.Addressee.Should().Be(TicketHoldAddressees.Plan);
    }

    [Fact]
    public async Task List_TwentyFiveSeriesTickets_ReadTheMergeOrderOncePerProject()
    {
        var five = await QueriesAsync(5);
        using var larger = new TicketHoldListTests();
        var twentyFive = await larger.QueriesAsync(25);

        twentyFive.Should().Be(five, "the merge order is read set-based, never once per ticket");
        larger._series.Lists.Should().Be(1, "one series read for the list");
    }

    [Fact]
    public async Task TicketPage_ListAndSeriesPage_NameTheSameHold()
    {
        await SeriesAsync("sr-1", "1", "2");
        await _h.AddAsync(Run("r-1", "1"), Step("r-1"), Record("1"), Run("r-2", "2", status: RunStatuses.Failed), Step("r-2"));

        var row = (await ListAsync()).Tickets.Single(t => t.TicketId == "2");
        var page = (await _h.Detail.ReadAsync("alpha", "2", default))!.Summary;
        await using var scope = _h.Services.CreateAsyncScope();
        var series = (await scope.ServiceProvider.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default))!;

        row.Hold.Should().NotBeNull().And.Be(page.Hold).And.Be(series.Tickets.Single(t => t.TicketId == "2").Hold);
        series.Tickets.Single(t => t.TicketId == "1").Hold!.Case.Should().Be(TicketHoldCases.MergeNext);
        series.Counts.Held.Should().Be(1, "the failed ticket is held; the next merge is not counted as held");
        series.Tickets.Select(t => (t.TicketId, t.Kind)).Should().BeEquivalentTo([("1", "now"), ("2", "failed")]); // 2026-11-25-9992a
    }

    [Fact]
    public async Task List_AShortDeliveryWithoutAPullRequest_IsAPersonsDecision_AFullOneIsDone()
    {
        await _h.AddAsync(Run("r-1", "41", status: RunStatuses.Shortfall), Step("r-1"), Run("r-2", "42"), Step("r-2"));

        var list = await ListAsync();

        list.Tickets.Single(t => t.TicketId == "41").Should().Match<TicketSummaryView>(t =>
            t.Bucket == TicketBucket.NeedsYou && t.Hold!.Case == TicketHoldCases.Shortfall);
        list.Tickets.Single(t => t.TicketId == "42").Should().Match<TicketSummaryView>(t => t.Bucket == TicketBucket.Done && t.Hold == null);
    }

    [Fact]
    public void HeldNeverRun_AClosedRequirement_SaysNoOneWillFinishIt()
    {
        var row = new TicketSummaryView("alpha", "60", null, null, null, TicketBucket.Blocked, null, T, 0, 0m, "", "", null, null, null,
            0, 0, [], [], WaitsFor: [new TicketWaitsForView("58", "alpha", "#58", "Orders", Closed: true)]);

        _h.Services.GetRequiredService<TicketSummaryComposer>().HeldNeverRun(row, TicketHoldInputs.None)
            .Should().Match<TicketSummaryView>(r => r.Bucket == TicketBucket.Blocked && r.Hold == null);
    }

    [Fact]
    public void HeldNeverRun_AStartAfterAMerge_AndARunningRequirement_WaitOnAnother()
    {
        var composer = _h.Services.GetRequiredService<TicketSummaryComposer>();
        var row = new TicketSummaryView("alpha", "60", null, null, null, TicketBucket.Blocked, null, T, 0, 0m, "", "", null, null, null,
            0, 0, [], [], WaitsFor: [new TicketWaitsForView("58", "alpha", "#58", "Orders", StartsOnMerge: true)]);

        var merge = composer.HeldNeverRun(row, TicketHoldInputs.None);
        var running = composer.HeldNeverRun(row with { WaitsFor = [new TicketWaitsForView("58", "alpha", "#58", "Orders")] }, TicketHoldInputs.None);

        // 2026-11-23-849e: a ticket that starts once others merge is nobody's move — the series has one merge, its next.
        (merge.Bucket, merge.Hold!.Addressee, merge.Hold.Case, merge.Hold.Names.Single(), merge.Hold.Merges).Should()
            .Be((TicketBucket.Blocked, TicketHoldAddressees.Nobody, TicketHoldCases.StartsOnMerge, "#58", (int?)1));
        (running.Bucket, running.Hold!.Case).Should().Be((TicketBucket.Blocked, TicketHoldCases.Prerequisite));
    }

    private async Task<int> QueriesAsync(int tickets)
    {
        var ids = Enumerable.Range(1, tickets).Select(i => i.ToString()).ToArray();
        await SeriesAsync("sr-1", ids);
        foreach (var id in ids) await _h.AddAsync(Run($"r-{id}", id), Step($"r-{id}"), Record(id));
        _h.Commands.Reset();
        _series.Lists = 0;
        (await ListAsync()).Tickets.Should().HaveCount(tickets);
        return _h.Commands.Count;
    }

    private Task<TicketListView> ListAsync() =>
        _h.List.ReadAsync(new TicketListRequest(ProjectScope.Single("alpha"), null, null, null), default);

    private async Task LedgerAsync(Func<ReworkLedgerRepository, Task> write)
    {
        await using var scope = _h.Services.CreateAsyncScope();
        await write(scope.ServiceProvider.GetRequiredService<ReworkLedgerRepository>());
    }

    private Task SeriesAsync(string id, params string[] tickets) =>
        _h.Services.GetRequiredService<ISeriesRunStore>().OpenAsync(new SeriesRunRecord(id, $"conv-{id}", "alpha", "operator", true,
            SeriesRunStates.Running, [.. tickets.Select(t => new SeriesTicket(t, $"SCRUM-{t}", $"Ticket {t}", false, T))], T), default);

    private static PrReviewRecord Record(string ticket)
    {
        var url = $"https://github.com/o/api/pull/{ticket}";
        return new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = "api", RepoKey = RepoKey.Of("https://github.com/o/api"), Number = ticket,
            Project = "alpha", TicketId = ticket, RunId = $"r-{ticket}", State = PrReviewStates.Open, HeadSha = "abc",
            CheckedAtTicks = T.UtcTicks,
        };
    }

    private static PrReviewRecord Record(string ticket, string ci)
    {
        var record = Record(ticket);
        record.CiState = ci;
        record.CiHeadSha = record.HeadSha;
        record.CiAtTicks = T.UtcTicks;
        record.CiFailuresJson = ci == CiStates.Red ? PrRecordCiJson.Failures([new CiFailure("build", null, "exit 1")]) : null;
        return record;
    }

    private static TicketRequirement Requires(string ticket, string required) => new()
    {
        Tracker = "gh", TicketKey = $"github-{ticket}", TicketId = ticket, Project = "alpha",
        RequiredTicketKey = $"github-{required}", RequiredTicketId = required, FiledInConversation = "conv",
    };

    /// <summary>The real series store, counting the list reads.</summary>
    private sealed class SeriesRunRepositoryCounter
    {
        public int Lists { get; set; }

        public ISeriesRunStore Bind(ISeriesRunStore inner)
        {
            var store = new Mock<ISeriesRunStore>();
            store.Setup(s => s.OpenAsync(It.IsAny<SeriesRunRecord>(), It.IsAny<CancellationToken>()))
                .Returns<SeriesRunRecord, CancellationToken>(inner.OpenAsync);
            store.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<string, CancellationToken>(inner.GetAsync);
            store.Setup(s => s.ForTicketAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, string, CancellationToken>(inner.ForTicketAsync);
            store.Setup(s => s.ListAsync(It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyCollection<string>?, CancellationToken>((projects, ct) => { Lists++; return inner.ListAsync(projects, ct); });
            return store.Object;
        }
    }
}
