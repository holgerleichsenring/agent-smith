using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-24-629aa: after a refresh, an open pull request whose required build has not started on its head for
/// merge.ci_settle_seconds gets one merge-green run that queues it again — under every review — and its head is marked
/// acted, so the next refresh starts nothing; a draft, a pending head and a held lease start nothing.
/// </summary>
public sealed class CiBuildRequeueTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly List<CiGreenMergeRequest> _launched = [];
    private ResolvedProject _project = Project(ReviewMode.End);
    private string _ci = CiStates.NotStarted;
    private string _head = "abc";
    private bool _draft;
    private bool _ran = true;

    public CiBuildRequeueTests()
    {
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
                new PrReviewState(url.Split('/')[^1], PrReviewStates.Open, PrReviewRefreshHarness.T, [], _head) { IsDraft = _draft });
        _h.Provider.As<IPullRequestCiReader>().Setup(c => c.ReadCiAsync(It.IsAny<string>(), It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PullRequestCi(_ci, _head, null, []));
        _h.Provider.Setup(p => p.ReadPullRequestBaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("main");
        _h.MergeRuns.Setup(r => r.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>(),
                It.IsAny<ActSlot<IReadOnlyList<PullRequestMergeLine>>?>()))
            .Callback<ResolvedProject, CiGreenMergeRequest, CancellationToken, ActSlot<IReadOnlyList<PullRequestMergeLine>>?>((_, r, _, _) => _launched.Add(r))
            .ReturnsAsync(() => _ran);
        _h.Attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("r1", RunStatuses.Success, PrReviewRefreshHarness.T, true)
            {
                PullRequestUrls = new Dictionary<string, string> { ["api"] = PrReviewRefreshHarness.Pr1 },
            });
    }

    [Theory]
    [InlineData(ReviewMode.End)]
    [InlineData(ReviewMode.Each)]
    [InlineData(ReviewMode.None)]
    public async Task ANotStartedHead_PastTheSettle_IsQueuedAgainOnce(ReviewMode when)
    {
        _project = Project(when);
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        _launched.Should().BeEmpty("a host queues the build a while after a push");

        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);
        await RefreshAsync();
        await RefreshAsync();

        var request = _launched.Should().ContainSingle().Subject;
        request.AttemptRunId.Should().Be("r1");
        request.PullRequests.Should().ContainSingle().Which.Should().Match<CiGreenPullRequest>(p => p.NotStarted && !p.NoCi && p.Repo == "api");
        _h.Records().Single().CiActed.Should().StartWith("abc:not_started@");
    }

    [Fact]
    public async Task ANewHead_IsQueuedAgain()
    {
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);
        await RefreshAsync();

        _head = "def";
        _h.Clock.Now = _h.Clock.Now.AddSeconds(1);
        await RefreshAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);
        await RefreshAsync();

        _launched.Should().HaveCount(2);
        _h.Records().Single().CiActed.Should().StartWith("def:not_started@");
    }

    [Fact]
    public async Task ARequeueThatDidNotTake_IsNotRepeated_ButANewSpellOnTheSameHeadIs()
    {
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);
        await RefreshAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(600);
        await RefreshAsync();
        _launched.Should().ContainSingle("a queue that did not take is a person's to look at, not a loop's");

        _ci = CiStates.Green; // the build ran; hours later its evaluation expired on the same head
        _h.Clock.Now = _h.Clock.Now.AddSeconds(1);
        await RefreshAsync();
        _ci = CiStates.NotStarted;
        _h.Clock.Now = _h.Clock.Now.AddSeconds(1);
        await RefreshAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);
        await RefreshAsync();

        _launched.Should().HaveCount(2);
    }

    [Fact]
    public async Task AConflictingPullRequest_IsLeftToTheConflictRefresh()
    {
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
                new PrReviewState(url.Split('/')[^1], PrReviewStates.Open, PrReviewRefreshHarness.T, [], _head) { HasConflicts = true });
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);

        await RefreshAsync();

        _launched.Should().NotContain(r => r.PullRequests.Any(p => p.NotStarted));
    }

    [Fact]
    public async Task ADraft_APendingHead_OrAHeldLease_StartNothing()
    {
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        _draft = true;
        await RefreshAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);
        await RefreshAsync();
        _launched.Should().BeEmpty("a draft runs no build policy; publishing it queues the build");

        _draft = false;
        _ci = CiStates.Pending;
        await RefreshAsync();
        _launched.Should().BeEmpty("a running build needs nothing");

        _ci = CiStates.NotStarted;
        _h.Leases.Setup(l => l.GetByTicketAsync("p", new TicketId("7"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSmith.Contracts.Models.StaleLease("p", new TicketId("7"), "other", null));
        await RefreshAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);
        await RefreshAsync();
        _launched.Should().BeEmpty();
    }

    [Fact]
    public async Task ARunThatDidNotRun_MarksNothing_SoTheNextRefreshTriesAgain()
    {
        _ran = false;
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);

        await RefreshAsync();
        await RefreshAsync();

        _launched.Should().HaveCount(2);
        _h.Records().Single().CiActed.Should().BeNull();
    }

    private Task RefreshAsync() => _h.Refresh().RefreshAsync(_project, "7", null, CancellationToken.None);

    private static ResolvedProject Project(ReviewMode when) => new()
    {
        Name = "p", Tracker = PrReviewRefreshHarness.Project.Tracker, Repos = PrReviewRefreshHarness.Project.Repos,
        Review = when,
    };

    public void Dispose() => _h.Dispose();
}
