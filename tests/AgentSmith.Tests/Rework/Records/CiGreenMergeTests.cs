using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
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
        _h.MergeRuns.Setup(r => r.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResolvedProject, CiGreenMergeRequest, CancellationToken>((_, r, _) => _launched.Add(r)).ReturnsAsync(true);
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
        _h.MergeRuns.Setup(r => r.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Ci(PrReviewRefreshHarness.Pr1, CiStates.Green);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await RefreshAsync();

        _h.Records().Single().CiActed.Should().BeNull();
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
