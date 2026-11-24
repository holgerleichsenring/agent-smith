using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-24-f9c5d: after a refresh, a pull request into a series branch that reported no CI on its head for
/// merge.ci_settle_seconds gets one merge-green run that starts its CI first — under every merge.series_when — and its head
/// is marked acted, so the next refresh starts nothing; a default-branch pull request is the CI merge's, as before.
/// </summary>
public sealed class SeriesCiStartTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly List<CiGreenMergeRequest> _launched = [];
    private ResolvedProject _project = Project(MergeWhen.Manual, MergeWhen.OnCiGreen);

    public SeriesCiStartTests()
    {
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) => new PrReviewState(url.Split('/')[^1], PrReviewStates.Open, PrReviewRefreshHarness.T, [], "abc"));
        _h.Provider.As<IPullRequestCiReader>().Setup(c => c.ReadCiAsync(It.IsAny<string>(), It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestCi(CiStates.None, "abc", null, []));
        _h.MergeRuns.Setup(r => r.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>(), It.IsAny<AgentSmith.Application.Services.TicketActs.ActSlot<IReadOnlyList<AgentSmith.Application.Models.PullRequestMergeLine>>?>()))
            .Callback<ResolvedProject, CiGreenMergeRequest, CancellationToken, AgentSmith.Application.Services.TicketActs.ActSlot<IReadOnlyList<AgentSmith.Application.Models.PullRequestMergeLine>>?>((_, r, _, _) => _launched.Add(r)).ReturnsAsync(true);
        _h.Attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("r1", RunStatuses.Success, PrReviewRefreshHarness.T, true)
            {
                PullRequestUrls = new Dictionary<string, string> { ["api"] = PrReviewRefreshHarness.Pr1 },
            });
    }

    [Theory]
    [InlineData(MergeWhen.Manual)]
    [InlineData(MergeWhen.OnCiGreen)]
    public async Task ASeriesHeadWithoutCi_PastTheSettle_StartsItsCiOnce(MergeWhen seriesWhen)
    {
        _project = Project(MergeWhen.Manual, seriesWhen);
        Target("agent-smith/series/sr-1");
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        _launched.Should().BeEmpty("a host registers its checks a while after a push");

        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);
        await RefreshAsync();
        await RefreshAsync();

        var request = _launched.Should().ContainSingle().Subject;
        request.SeriesBranch.Should().Be("agent-smith/series/sr-1");
        request.PullRequests.Should().ContainSingle().Which.NoCi.Should().BeTrue();
        _h.Records().Single().CiActed.Should().Be("abc:none");
    }

    [Fact]
    public async Task ADefaultBranchHeadWithoutCi_StartsNothingHere()
    {
        Target("main");
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        _h.Clock.Now = _h.Clock.Now.AddSeconds(181);

        await RefreshAsync();

        _launched.Should().BeEmpty();
    }

    private void Target(string branch) =>
        _h.Provider.Setup(p => p.ReadPullRequestBaseAsync(PrReviewRefreshHarness.Pr1, It.IsAny<CancellationToken>())).ReturnsAsync(branch);

    private Task RefreshAsync() => _h.Refresh().RefreshAsync(_project, "7", null, CancellationToken.None);

    private static ResolvedProject Project(MergeWhen when, MergeWhen seriesWhen) => new()
    {
        Name = "p", Tracker = PrReviewRefreshHarness.Project.Tracker, Repos = PrReviewRefreshHarness.Project.Repos,
        Merge = new MergePolicyConfig { When = when, SeriesWhen = seriesWhen },
    };

    public void Dispose() => _h.Dispose();
}
