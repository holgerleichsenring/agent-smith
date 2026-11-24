using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-17-3435b: after a refresh, a merge: on_ci_green ticket whose newest attempt finished green with its pull
/// requests awaiting CI starts one merge-green run once CI is green on every one of them; each head and verdict is
/// acted on once, and only a run that ran marks it.
/// </summary>
public sealed class CiGreenMergeTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly List<CiGreenMergeRequest> _launched = [];
    private ResolvedProject _project = Project(MergeWhen.OnCiGreen);

    public CiGreenMergeTests()
    {
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) => new PrReviewState(url.Split('/')[^1], PrReviewStates.Open, PrReviewRefreshHarness.T, [], "abc"));
        _h.MergeRuns.Setup(r => r.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>(), It.IsAny<AgentSmith.Application.Services.TicketActs.ActSlot<IReadOnlyList<AgentSmith.Application.Models.PullRequestMergeLine>>?>()))
            .Callback<ResolvedProject, CiGreenMergeRequest, CancellationToken, AgentSmith.Application.Services.TicketActs.ActSlot<IReadOnlyList<AgentSmith.Application.Models.PullRequestMergeLine>>?>((_, r, _, _) => _launched.Add(r)).ReturnsAsync(true);
        Attempt(RunStatuses.Success, PrReviewRefreshHarness.Pr1);
    }

    [Fact]
    public async Task AllAwaitedGreen_LaunchesOnce_AndMarksActed()
    {
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Green);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await RefreshAsync();
        await RefreshAsync();

        _launched.Should().ContainSingle().Which.Should().BeEquivalentTo(new CiGreenMergeRequest("7", "r1", "agent-smith/7",
            [new CiGreenPullRequest("api", PrReviewRefreshHarness.Pr1)]));
        _h.Records().Single().CiActed.Should().Be("abc:green");
    }

    [Fact]
    public async Task ARefreshWithoutTheCiLoop_LaunchesNothing_SoAnActsRequestNeverWaitsOnAPipeline()
    {
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Green);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await _h.Refresh().RefreshAsync(_project, "7", null, CancellationToken.None, ciLoop: false); // 2026-11-24-a4e7

        _launched.Should().BeEmpty();
        _h.Records().Single().CiActed.Should().BeNull("the worker's next refresh acts on it");
    }

    [Theory]
    [InlineData(CiStates.Pending)]
    [InlineData(CiStates.Red)]
    public async Task NotGreen_LaunchesNothing(string state)
    {
        Ci(PrReviewRefreshHarness.Pr1, state);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await RefreshAsync();

        _launched.Should().BeEmpty();
    }

    [Fact]
    public async Task AnOlderAttemptsClosedPullRequest_DoesNotBlock()
    {
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Green);
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(PrReviewRefreshHarness.Pr2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState("2", PrReviewStates.Closed, PrReviewRefreshHarness.T, [], "old"));
        await _h.RunAsync("r0", PrReviewRefreshHarness.Pr2);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await RefreshAsync();

        _launched.Should().ContainSingle();
    }

    [Fact]
    public async Task ALiveRun_AFailedAttempt_OrAProjectNotOnCiGreen_LaunchNothing()
    {
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Green);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        _project = Project(MergeWhen.OnGateGreen);
        await RefreshAsync();
        _project = Project(MergeWhen.OnCiGreen);
        Attempt(RunStatuses.Failed, PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        Attempt(RunStatuses.Success, PrReviewRefreshHarness.Pr1);
        _h.Leases.Setup(l => l.GetByTicketAsync("p", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSmith.Contracts.Models.StaleLease("p", new TicketId("7"), "live-run", null));
        await RefreshAsync();

        _launched.Should().BeEmpty();
    }

    [Fact]
    public async Task NoCi_MergesOnlyAfterTheSettleTime()
    {
        Ci(PrReviewRefreshHarness.Pr1, CiStates.None);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        _launched.Should().BeEmpty();

        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);
        await RefreshAsync();

        _launched.Should().ContainSingle();
        _h.Records().Single().CiActed.Should().Be("abc:none");
    }

    [Fact]
    public async Task RedThenGreenOnTheSameHead_Merges()
    {
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Red);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Green);
        _h.Clock.Now = _h.Clock.Now.AddMinutes(1);

        await RefreshAsync();

        _launched.Should().ContainSingle();
    }

    [Fact]
    public async Task ALeaseHeldByAnother_MarksNothing_SoTheNextRefreshTriesAgain()
    {
        _h.MergeRuns.Setup(r => r.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>(), It.IsAny<AgentSmith.Application.Services.TicketActs.ActSlot<IReadOnlyList<AgentSmith.Application.Models.PullRequestMergeLine>>?>())).ReturnsAsync(false);
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Green);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await RefreshAsync();

        _h.Records().Single().CiActed.Should().BeNull();
    }

    // 2026-11-23-97c2a: the merge-green run refuses while a required ticket has not landed (CompletePullRequestsHandlerTests);
    // its mark keeps the refusal from repeating on every refresh, and the required ticket's landing asks once more.
    [Fact]
    public async Task ARefusedMerge_IsNotRetried_UntilTheRequiredTicketLands()
    {
        await RequiresAsync("7", "5");
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Green);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        await RefreshAsync();
        _launched.Should().ContainSingle();

        await _h.Recorder().RecordByHandAsync(_project, "5", "ops", "merged elsewhere", CancellationToken.None);
        _h.Records().Single().CiActed.Should().BeNull("the landing cleared the mark");
        await RefreshAsync();

        _launched.Should().HaveCount(2);
    }

    [Fact]
    public async Task ALanding_KeepsARedVerdictsMark()
    {
        await RequiresAsync("7", "5");
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Red);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        var red = _h.Records().Single().CiActed;

        await _h.Recorder().RecordByHandAsync(_project, "5", "ops", "merged elsewhere", CancellationToken.None);

        _h.Records().Single().CiActed.Should().Be(red).And.Be("abc:red", "the fix nudge acted on it and a landing changes nothing about it");
    }

    private async Task RequiresAsync(string dependent, string required)
    {
        await using var db = _h.Store.Context();
        db.Add(new TicketRequirement
        {
            Tracker = "gh-tracker", TicketKey = $"github-{dependent}", TicketId = dependent, Project = "p",
            RequiredTicketKey = $"github-{required}", RequiredTicketId = required, StartsWhenMet = true, FiledInConversation = "s-1",
        });
        await db.SaveChangesAsync();
    }

    private Task RefreshAsync() => _h.Refresh().RefreshAsync(_project, "7", null, CancellationToken.None);

    private void Ci(string url, string state) =>
        _h.Provider.As<IPullRequestCiReader>().Setup(c => c.ReadCiAsync(url, It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestCi(state, "abc", null, []));

    private void Attempt(string status, string url) =>
        _h.Attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("r1", status, PrReviewRefreshHarness.T, true)
            {
                AwaitingCiUrls = new Dictionary<string, string> { ["api"] = url },
            });

    private static ResolvedProject Project(MergeWhen when) => new()
    {
        Name = "p", Tracker = PrReviewRefreshHarness.Project.Tracker, Repos = PrReviewRefreshHarness.Project.Repos,
        Merge = new MergePolicyConfig { When = when },
    };

    public void Dispose() => _h.Dispose();
}
