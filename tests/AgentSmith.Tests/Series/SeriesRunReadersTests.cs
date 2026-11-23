using AgentSmith.Application.Services.Series;
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
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-31dcb: the series list and page over a real migrated store — CI per repo, merge order, the name,
/// the caller's projects, and a list whose query count does not grow with its tickets.</summary>
public sealed class SeriesRunReadersTests : IDisposable
{
    private static readonly DateTimeOffset T = TicketStoreHarness.T;
    private readonly TicketStoreHarness _h = new(Register);

    public SeriesRunReadersTests() => _h.Config.Projects["alpha"] = new()
    {
        Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
        Repos = [new RepoConnection { Name = "api", DefaultBranch = "main" }, new RepoConnection { Name = "web", DefaultBranch = "main" }],
    };

    [Fact]
    public async Task Detail_TwoReposOfOneTicket_CarryTheirOwnCi()
    {
        await OpenAsync("sr-1", Tickets("1", "2"));
        await _h.AddAsync(
            Record("1", "web", CiStates.Red, failures: [new CiFailure("build", "https://ci/log/7", "error CS1002\nmore")]),
            Record("1", "api", CiStates.Green), Requires("2", "1"), TicketStoreHarness.Run("run-1", ticket: "1"));

        var detail = (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default)))!;

        var one = detail.Tickets.Single(t => t.TicketId == "1");
        one.Repos.Should().Equal(["api", "web"], "the project's repo order");
        one.PullRequests.Select(p => (p.Repo, p.Ci)).Should().Equal([("api", CiStates.Green), ("web", CiStates.Red)]);
        one.PullRequests[1].CiFailure.Should().Be("build: error CS1002");
        one.PullRequests[1].CiLog.Should().Be("https://ci/log/7");
        one.Status.Should().Be("success");
        detail.Tickets.Select(t => (t.TicketId, t.Depth)).Should().Equal([("1", 0), ("2", 1)]);
    }

    [Fact]
    public async Task Detail_MergeOrder_ReadyOnlyWhereEveryRequirementLanded()
    {
        await OpenAsync("sr-1", Tickets("1", "2", "3"));
        await _h.AddAsync(Record("1", "api", CiStates.Green), Record("2", "api", CiStates.Green), Requires("2", "1"),
            Landed("3"), TicketStoreHarness.Run("run-1", ticket: "1"));

        var detail = (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default)))!;

        detail.MergeOrder.Select(s => (s.Key, s.Wait)).Should().Equal(
            [("SCRUM-1", SeriesRunMergeWaits.Ready), ("SCRUM-2", SeriesRunMergeWaits.After)]);
        detail.MergeOrder[1].After.Should().Equal(["SCRUM-1"]);
        detail.Counts.Should().Be(new AgentSmith.Server.Models.SeriesRunCounts(3, 1, 2, 0, 0));
    }

    [Fact]
    public async Task Detail_APullRequestNotReadYet_WaitsAndIsInTheMergeOrder()
    {
        await OpenAsync("sr-1", Tickets("1"));
        var unread = Record("1", "api", CiStates.Pending);
        unread.State = PrReviewStates.Unread;
        await _h.AddAsync(unread);

        var detail = (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default)))!;

        detail.Counts.Waiting.Should().Be(1);
        detail.MergeOrder.Should().ContainSingle().Which.Wait.Should().Be(SeriesRunMergeWaits.Ready);
    }

    [Fact]
    public async Task ByTicketsAsync_ReadsTheNamedTicketsOfTheProjectOnly()
    {
        await _h.AddAsync(Record("1", "api", CiStates.Green), Record("2", "api", CiStates.Green));

        var read = await ReadAsync(s => s.GetRequiredService<PrReviewRecordRepository>().ByTicketsAsync("alpha", ["1"], default));

        read.Should().ContainSingle().Which.Ci!.State.Should().Be(CiStates.Green);
        (await ReadAsync(s => s.GetRequiredService<PrReviewRecordRepository>().ByTicketsAsync("beta", ["1"], default))).Should().BeEmpty();
    }

    [Fact]
    public async Task Detail_AStoppedAttemptWithNothingToMerge_IsHeld()
    {
        await OpenAsync("sr-1", Tickets("1", "2", "3"));
        await _h.AddAsync(TicketStoreHarness.Run("run-1", ticket: "1", status: "failed"),
            TicketStoreHarness.Run("run-2", ticket: "2", status: "running"));

        var counts = (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default)))!.Counts;

        counts.Should().Be(new AgentSmith.Server.Models.SeriesRunCounts(3, 0, 0, 1, 1), "1 failed is held, 2 works, 3 never started");
    }

    [Fact]
    public async Task Detail_CarriesTheConversationsThreadReposAndCost()
    {
        await OpenAsync("sr-1", Tickets("1", "2"));
        await _h.AddAsync(Record("1", "web", CiStates.Green), Record("2", "api", CiStates.Green),
            TicketStoreHarness.Run("run-1", ticket: "1", cost: 1.25m), TicketStoreHarness.Run("run-9", ticket: "9", cost: 7m),
            new SpecDialogSession
            {
                SessionId = "conv-sr-1", Platform = "dashboard", ChannelId = "c", ThreadId = "d-thread1", UserId = "u", LastActivityAt = T,
            });

        var detail = (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default)))!;

        detail.ThreadId.Should().Be("d-thread1");
        detail.ConversationOpen.Should().BeTrue();
        detail.Repos.Should().Equal(["api", "web"], "the project's repo order, not the tickets'");
        detail.CostUsd.Should().Be(1.25m, "ticket 9 is not of the series");
    }

    [Fact]
    public async Task Detail_AClosedConversation_IsNotOpen_AndAMissingOneHasNoThread()
    {
        await OpenAsync("sr-1", Tickets("1"));
        await OpenAsync("sr-2", Tickets("2"));
        await _h.AddAsync(new SpecDialogSession
        {
            SessionId = "conv-sr-1", Platform = "dashboard", ChannelId = "c", ThreadId = "d-closed", UserId = "u", IsOpen = false,
            LastActivityAt = T,
        });

        var closed = (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default)))!;
        var gone = (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-2", default)))!;

        (closed.ThreadId, closed.ConversationOpen).Should().Be(("d-closed", false));
        (gone.ThreadId, gone.ConversationOpen).Should().Be(((string?)null, false));
        gone.CostUsd.Should().Be(0m);
    }

    [Fact]
    public async Task SeriesRunCosts_SumsTheNamedTicketsOfTheProjectOnly()
    {
        await _h.AddAsync(TicketStoreHarness.Run("run-1", ticket: "1", cost: 1.5m), TicketStoreHarness.Run("run-2", ticket: "1", cost: 2m),
            TicketStoreHarness.Run("run-3", ticket: "2", cost: 4m), TicketStoreHarness.Run("run-4", ticket: "1", project: "beta", cost: 8m));

        var cost = await ReadAsync(s => s.GetRequiredService<SeriesRunCosts>().OfAsync("alpha", ["1"], default));

        cost.Should().Be(3.5m);
    }

    [Fact]
    public async Task Detail_CarriesEachEdgesKind_FromThePerRepoStack()
    {
        await OpenAsync("sr-1", Tickets("1", "2", "3", "4"));
        await _h.AddAsync(Requires("2", "1"), Requires("4", "3"), Landed("3"), WithPr(TicketStoreHarness.Run("run-1", ticket: "1")),
            TicketStoreHarness.Run("run-9", ticket: "9", status: "running"));

        var edges = (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default)))!.Edges;

        edges.Should().BeEquivalentTo(
            [new SeriesRunEdgeView("2", "1", SeriesRunEdgeKinds.Stacked), new SeriesRunEdgeView("4", "3", SeriesRunEdgeKinds.Landed)]);
    }

    [Fact]
    public async Task Detail_APrerequisiteWithoutAGreenAttempt_IsAnUnknownEdge()
    {
        await OpenAsync("sr-1", Tickets("1", "2"));
        await _h.AddAsync(Requires("2", "1"), TicketStoreHarness.Run("run-1", ticket: "1", status: "running"));

        var edges = (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default)))!.Edges;

        edges.Should().ContainSingle().Which.Kind.Should().Be(SeriesRunEdgeKinds.Unknown);
    }

    [Fact]
    public async Task Detail_UnknownSeries_IsNull()
    {
        (await ReadAsync(s => s.GetRequiredService<SeriesRunDetailReader>().ReadAsync("none", default))).Should().BeNull();
    }

    [Fact]
    public async Task List_NamesTheSeriesByItsConversation_AndLeavesOutProjectsTheCallerCannotSee()
    {
        await OpenAsync("sr-1", Tickets("1"));
        await _h.AddAsync(new SpecDialogSession
        {
            SessionId = "conv-sr-1", Platform = "dashboard", ChannelId = "c", ThreadId = "t", UserId = "u",
            Subject = "Approval workflow", LastActivityAt = T,
        });

        var seen = await ReadAsync(s => s.GetRequiredService<SeriesRunOverviewReader>().ReadAsync(ProjectScope.Everything, default));
        var hidden = await ReadAsync(s => s.GetRequiredService<SeriesRunOverviewReader>().ReadAsync(ProjectScope.Of(["beta"]), default));

        seen.Should().ContainSingle().Which.Name.Should().Be("Approval workflow");
        seen[0].Counts.NotStarted.Should().Be(1);
        hidden.Should().BeEmpty("a series of a project outside the caller's scope is not listed");
    }

    [Fact]
    public async Task List_TwentyFiveTickets_SendsAsManyQueriesAsFive()
    {
        var five = await ListQueriesAsync(5);

        using var larger = new SeriesRunReadersTests();
        var twentyFive = await larger.ListQueriesAsync(25);

        twentyFive.Should().Be(five, "the list reads set-based, never once per ticket");
    }

    public void Dispose() => _h.Dispose();

    private async Task<int> ListQueriesAsync(int tickets)
    {
        var ids = Enumerable.Range(1, tickets).Select(i => i.ToString()).ToArray();
        await OpenAsync("sr-1", Tickets(ids));
        foreach (var id in ids.Skip(1))
            await _h.AddAsync(Record(id, "api", CiStates.Green), Requires(id, "1"), TicketStoreHarness.Run($"run-{id}", ticket: id));
        _h.Commands.Reset();
        var rows = await ReadAsync(s => s.GetRequiredService<SeriesRunOverviewReader>().ReadAsync(ProjectScope.Everything, default));
        rows.Single().Counts.Total.Should().Be(tickets);
        return _h.Commands.Count;
    }

    private static void Register(IServiceCollection services)
    {
        services.AddSingleton<ISeriesRunStore, SeriesRunRepository>().AddTransient<SeriesRunOrder>();
        services.AddTransient<SeriesRunPullRequests>().AddTransient<SeriesRunTickets>();
        services.AddScoped<SeriesRunProjectReads>().AddScoped<SeriesRunNames>().AddScoped<SeriesRunCosts>()
            .AddScoped<SeriesRunOverviewReader>().AddScoped<SeriesRunDetailReader>();
        services.AddSingleton<AgentSmith.Application.Services.Claim.GreenPrerequisites>().AddScoped<SeriesRunEdges>(); // 31dcf
        services.AddTransient<SeriesRunHolds>(); // 31dcg
    }

    private async Task<TResult> ReadAsync<TResult>(Func<IServiceProvider, Task<TResult>> read)
    {
        await using var scope = _h.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider);
    }

    private Task OpenAsync(string id, IReadOnlyList<SeriesTicket> tickets) =>
        _h.Services.GetRequiredService<ISeriesRunStore>().OpenAsync(
            new SeriesRunRecord(id, $"conv-{id}", "alpha", "operator", true, SeriesRunStates.Running, tickets, T), default);

    private static IReadOnlyList<SeriesTicket> Tickets(params string[] ids) =>
        [.. ids.Select(id => new SeriesTicket(id, $"SCRUM-{id}", $"Ticket {id}", false, T))];

    private static PrReviewRecord Record(string ticket, string repo, string ci, IReadOnlyList<CiFailure>? failures = null)
    {
        var url = $"https://github.com/o/{repo}/pull/{ticket}";
        return new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = repo, RepoKey = RepoKey.Of($"https://github.com/o/{repo}"), Number = ticket,
            Project = "alpha", TicketId = ticket, RunId = $"run-{ticket}", State = PrReviewStates.Open, HeadSha = "abc",
            CheckedAtTicks = T.UtcTicks, CiState = ci, CiHeadSha = "abc", CiFailuresJson = PrRecordCiJson.Failures(failures ?? []),
        };
    }

    private static Run WithPr(Run run)
    {
        run.PullRequestsJson = RunStoryJson.Serialize(new List<RunPullRequestView>
            { new("api", PullRequestStatuses.Opened, $"https://github.com/o/api/pull/{run.TicketId}", null, T) });
        return run;
    }

    private static TicketRequirement Requires(string ticket, string required) => new()
    {
        Tracker = "gh", TicketKey = $"github-{ticket}", TicketId = ticket, Project = "alpha",
        RequiredTicketKey = $"github-{required}", RequiredTicketId = required, FiledInConversation = "conv",
    };

    private static LandedTicket Landed(string ticket) => new()
    {
        Tracker = "gh", TicketKey = $"github-{ticket}", TicketId = ticket, Project = "alpha", Kind = "pr", LandedAt = T,
    };
}
