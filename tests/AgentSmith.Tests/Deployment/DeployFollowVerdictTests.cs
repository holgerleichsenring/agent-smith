using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Deployment;
using FluentAssertions;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863f: when a followed deploy has settled, and when it is read next.</summary>
public sealed class DeployFollowVerdictTests
{
    private static readonly DateTimeOffset Delivered = DateTimeOffset.Parse("2026-11-18T10:00:00Z");
    private static readonly MergePolicyConfig Merge = new() { CiPollSeconds = 60, CiPendingSeconds = 3600 };

    [Theory]
    [InlineData(CiStates.Green, true)]
    [InlineData(CiStates.Red, true)]
    [InlineData(CiStates.Pending, false)]
    public void Of_PipelineRun_SettlesOnceNotPending(string state, bool settled) =>
        DeployFollowVerdict.Of(Pipeline(state), Merge, Delivered.AddMinutes(5)).Settled.Should().Be(settled);

    [Fact]
    public void Of_PendingPastCiPendingSeconds_SettlesAndSaysWhy()
    {
        var (settled, note) = DeployFollowVerdict.Of(Pipeline(CiStates.Pending), Merge, Delivered.AddHours(2));

        settled.Should().BeTrue();
        note.Should().Contain("merge.ci_pending_seconds = 3600 s");
    }

    [Theory]
    [InlineData(PrReviewStates.Open, CiStates.Green, false)]
    [InlineData(PrReviewStates.Merged, CiStates.Pending, false)]
    [InlineData(PrReviewStates.Merged, CiStates.Green, true)]
    [InlineData(PrReviewStates.Closed, CiStates.Red, true)]
    public void Of_PullRequest_SettlesOnceEndedAndItsCiIsNotPending(string pr, string ci, bool settled) =>
        DeployFollowVerdict.Of(PullRequest(pr, ci), Merge, Delivered.AddMinutes(5)).Settled.Should().Be(settled);

    [Fact]
    public void IsDue_PendingByCiPoll_AnOpenPullRequestWithSettledCiByTheReReadInterval()
    {
        var tickets = new TicketsConfig { PrRereadIntervalSeconds = 900 };
        var pending = PullRequest(PrReviewStates.Open, CiStates.Pending, checkedAt: Delivered.AddMinutes(1));
        var waiting = PullRequest(PrReviewStates.Open, CiStates.Green, checkedAt: Delivered.AddMinutes(1));

        DeployFollowVerdict.IsDue(pending, Merge, tickets, Delivered.AddMinutes(2)).Should().BeTrue();
        DeployFollowVerdict.IsDue(waiting, Merge, tickets, Delivered.AddMinutes(2)).Should().BeFalse();
        DeployFollowVerdict.IsDue(waiting, Merge, tickets, Delivered.AddMinutes(16)).Should().BeTrue();
    }

    internal static DeployRecord Pipeline(string state) => new()
    {
        RunId = "r-1", PipelineRunId = "42", CiState = state, Url = "https://ci.example/runs/42", DeliveredAtTicks = Delivered.UtcTicks,
    };

    internal static DeployRecord PullRequest(string pr, string ci, DateTimeOffset? checkedAt = null) => new()
    {
        RunId = "r-1", PullRequestUrl = "https://stub.test/gitops/pull/2", PullRequestState = pr, CiState = ci,
        DeliveredAtTicks = Delivered.UtcTicks, CheckedAtTicks = checkedAt?.UtcTicks,
    };
}
