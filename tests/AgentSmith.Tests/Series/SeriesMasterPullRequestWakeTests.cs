using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Tests.Rework.Records;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-27-e451: a series under review end whose ticket pull request into the series branch reports no CI (no build
/// policy there) — the pull-request reads over a real migrated store record nothing while 'nothing yet' may still become a build,
/// then, once the head stood for merge.ci_settle_seconds, wake the master once with the verdict none; the beat hands that wake to
/// the master, and its merge_pull_request reaches the merge act for the ticket. A conflict appearing on the pull request wakes the
/// master the same way, and its refresh_branch reaches the refresh act.</summary>
public sealed class SeriesMasterPullRequestWakeTests : IDisposable
{
    private const string Branch = "agent-smith/series/sr-1";
    private readonly PrReviewRefreshHarness _h = new();
    private bool? _conflicts = false;

    public SeriesMasterPullRequestWakeTests()
    {
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) => new PrReviewState(url.Split('/')[^1], PrReviewStates.Open, Opened, [], "abc1234ff") { HasConflicts = _conflicts });
        _h.Provider.As<IPullRequestCiReader>().Setup(c => c.ReadCiAsync(It.IsAny<string>(), It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => PullRequestCi.NoneOn("abc1234ff"));
        _h.Provider.Setup(p => p.ReadPullRequestBaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Branch);
    }

    // The pull request opened when the clock started.
    private static DateTimeOffset Opened => PrReviewRefreshHarness.T.AddHours(1);

    public void Dispose() => _h.Dispose();

    private Task RefreshAtAsync(TimeSpan after)
    {
        _h.Clock.Now = Opened + after;
        return _h.Refresh().RefreshAsync(PrReviewRefreshHarness.Project, "7", null, CancellationToken.None);
    }

    [Fact]
    public async Task ASettledNoCiHead_WakesTheMaster_WhoseMergeReachesTheMergeAct()
    {
        await _h.Series.OpenAsync(SeriesRecords.Running() with
        {
            Project = "p", Tickets = [SeriesRecords.Ticket("7")], IntegrationBranch = Branch, Review = ReviewMode.End,
        }, default);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await RefreshAtAsync(TimeSpan.FromSeconds(10));
        await RefreshAtAsync(TimeSpan.FromMinutes(1));
        var early = (await _h.Series.GetAsync("sr-1", default))!.Wakes;
        early.Should().NotContain(w => w.Kind == SeriesWakeKinds.Ci, "a head that reports nothing yet may still get its build");

        await RefreshAtAsync(TimeSpan.FromMinutes(4)); // past merge.ci_settle_seconds = 180
        await RefreshAtAsync(TimeSpan.FromMinutes(5)); // read again: raised once per pull request and head
        var record = (await _h.Series.GetAsync("sr-1", default))!;
        var none = record.Wakes.Where(w => w.Kind == SeriesWakeKinds.Ci).Should().ContainSingle().Subject;
        (none.Verdict, none.Quiet).Should().Be((CiStates.None, false));
        none.Text.Should().Contain("abc1234").And.Contain("merge.ci_settle_seconds = 180");
        SeriesBeatDue.Waking(record, none).Should().BeTrue("no filter takes a no-CI head for quiet");

        var rig = new SeriesWakeBeatRig { Facts = [Ready("7")] };
        var series = rig.Seen(record with { Project = "sample" }, none.At) with { Wakes = [none] };
        var now = none.At.AddMinutes(5);
        (await rig.DueAsync(series, now)).Should().Be(SeriesBeatDue.EventsArrived);
        rig.PullRequests.Setup(p => p.MergeAsync(It.IsAny<SeriesRunRecord>(), "7", BranchProtection.Bypass, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesMergeOutcome("api: merged", Moved: true));
        rig.Loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .Returns<AgenticLoopRequest, CancellationToken>(async (request, ct) =>
            {
                rig.Prompts.Add(request.UserPrompt);
                await request.Tools.OfType<AIFunction>().Single(t => t.Name == SeriesPullRequestTools.MergeTool).InvokeAsync(new AIFunctionArguments(
                    new Dictionary<string, object?> { ["ticket"] = "SCRUM-7", ["how"] = "now", ["reason"] = "no CI on the series branch" }), ct);
                return new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Merged SCRUM-7.")), TimeSpan.Zero);
            });

        var beat = await rig.BeatAsync(series, now);

        beat.Wakes.Should().ContainSingle().Which.Verdict.Should().Be(CiStates.None);
        rig.Prompts.Should().ContainSingle().Which.Should().Contain("is none");
        rig.PullRequests.Verify(p => p.MergeAsync(It.IsAny<SeriesRunRecord>(), "7", BranchProtection.Bypass, null, It.IsAny<CancellationToken>()), Times.Once);
        rig.Store.Records["sr-1"].Acts.Should().ContainSingle().Which.Text.Should().StartWith("merged SCRUM-7");
    }

    [Fact]
    public async Task AConflictAppearing_WakesTheMaster_WhoseRefreshReachesTheRefreshAct()
    {
        await _h.Series.OpenAsync(SeriesRecords.Running() with
        {
            Project = "p", Tickets = [SeriesRecords.Ticket("7")], IntegrationBranch = Branch, Review = ReviewMode.End,
        }, default);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAtAsync(TimeSpan.FromSeconds(10));

        _conflicts = true; // another ticket landed on the series branch
        await RefreshAtAsync(TimeSpan.FromMinutes(1));
        var record = (await _h.Series.GetAsync("sr-1", default))!;
        var conflict = record.Wakes.Where(w => w.Kind == SeriesWakeKinds.PullRequest && !w.Quiet).Should().ContainSingle().Subject;
        conflict.Text.Should().Contain("conflicts with its base");
        SeriesBeatDue.Waking(record, conflict).Should().BeTrue();

        var rig = new SeriesWakeBeatRig
        {
            Facts = [SeriesRecords.Fact("7", "success").WithPr("open", CiStates.Green).Held(TicketHoldAddressees.Nobody, TicketHoldCases.MasterRefresh)],
        };
        var series = rig.Seen(record with { Project = "sample" }, conflict.At) with { Wakes = [conflict] };
        var now = conflict.At.AddMinutes(5);
        (await rig.DueAsync(series, now)).Should().Be(SeriesBeatDue.EventsArrived);
        rig.PullRequests.Setup(p => p.RefreshAsync(It.IsAny<SeriesRunRecord>(), "7", It.IsAny<CancellationToken>())).ReturnsAsync("Refresh started.");
        rig.Loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .Returns<AgenticLoopRequest, CancellationToken>(async (request, ct) =>
            {
                rig.Prompts.Add(request.UserPrompt);
                await request.Tools.OfType<AIFunction>().Single(t => t.Name == SeriesPullRequestTools.RefreshTool).InvokeAsync(new AIFunctionArguments(
                    new Dictionary<string, object?> { ["ticket"] = "SCRUM-7", ["reason"] = "a sibling landed on the series branch" }), ct);
                return new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Refreshing SCRUM-7.")), TimeSpan.Zero);
            });

        await rig.BeatAsync(series, now);

        rig.Prompts.Should().ContainSingle().Which.Should().Contain("conflicts with its base");
        rig.PullRequests.Verify(p => p.RefreshAsync(It.IsAny<SeriesRunRecord>(), "7", It.IsAny<CancellationToken>()), Times.Once);
        rig.Store.Records["sr-1"].Acts.Should().ContainSingle().Which.Text.Should().StartWith("refreshed SCRUM-7");
    }

    // The ticket as its hold reads it under end: the master's next merge, its pull request reporting no CI.
    private static SeriesTicketFact Ready(string id) =>
        SeriesRecords.Fact(id, "success").WithPr("open", CiStates.None).Held(TicketHoldAddressees.Nobody, TicketHoldCases.Merging);
}
