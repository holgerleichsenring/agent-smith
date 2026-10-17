using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.ClosedBucket;

/// <summary>
/// 2026-10-15-114fb: the rule's closed arm sits after working and yields to Done exactly where Done answers; the
/// arms after done split the old idle remainder into stuck, PR closed, failed and idle.
/// </summary>
public sealed class TicketBucketRuleSplitTests
{
    private static readonly TicketLiveState Live = new(
        new Dictionary<string, int>(), new HashSet<string>(), new Dictionary<string, PendingQuestionInfo>());

    private static readonly TicketClosedMark Mark = new("gh", "github-42", "42", "alpha", T.AddHours(2), "Ada", null, [], false);

    private static TicketPullRequestView Pr(string state, double hours = 1) =>
        new("1", "https://github.com/o/api/pull/1", "api", state, TicketReviewDecisions.None, null, null, T.AddHours(hours), null);

    private static string Bucket(string status, IReadOnlyList<TicketPullRequestView> prs, TicketClosedMark? mark = null, bool stuck = false) =>
        new TicketBucketRule().Of(Run("r-1", status: status), Live, prs, stuck, closed: mark).Bucket;

    [Fact]
    public void TicketBucketRule_MarkInForceWithAnOpenPullRequest_IsClosedNotWaitingOnReview() =>
        Bucket("success", [Pr(PrReviewStates.Open)], Mark).Should().Be(TicketBucket.Closed);

    [Fact]
    public void TicketBucketRule_MarkButMergedWithNoLaterAttempt_IsDone() =>
        Bucket("success", [Pr(PrReviewStates.Merged)], Mark).Should().Be(TicketBucket.Done);

    [Fact]
    public void TicketBucketRule_MarkAfterAFailedReworkOfALandedTicket_IsClosed() =>
        Bucket("failed", [Pr(PrReviewStates.Merged, hours: -1)], Mark).Should().Be(TicketBucket.Closed);

    [Fact]
    public void TicketBucketRule_MarkWithARunningAttempt_IsWorking() =>
        Bucket("running", [], Mark).Should().Be(TicketBucket.Working);

    [Fact]
    public void TicketBucketRule_PullRequestsAllClosedUnmerged_IsPrClosed() =>
        Bucket("failed", [Pr(PrReviewStates.Closed), Pr(PrReviewStates.Closed)]).Should().Be(TicketBucket.PrClosed);

    [Fact]
    public void TicketBucketRule_FailedAttemptNoPullRequest_IsFailed() =>
        Bucket("failed", []).Should().Be(TicketBucket.Failed);

    [Fact]
    public void TicketBucketRule_FailedHeadWithAnOpenPullRequest_IsFailed() =>
        Bucket("failed", [Pr(PrReviewStates.Open)]).Should().Be(TicketBucket.Failed);

    [Fact]
    public void TicketBucketRule_SucceededHeadWithAnOpenPullRequest_StaysWaitingOnReview() =>
        Bucket("success", [Pr(PrReviewStates.Open)]).Should().Be(TicketBucket.WaitingOnReview);

    [Fact]
    public void TicketBucketRule_FailedHeadOpenPullRequestStuck_IsStuck() =>
        Bucket("failed", [Pr(PrReviewStates.Open)], stuck: true).Should().Be(TicketBucket.Stuck);

    [Fact]
    public void TicketBucketRule_CancelledAttempt_IsIdle() =>
        Bucket("cancelled", []).Should().Be(TicketBucket.Idle);

    [Fact]
    public void TicketBucketRule_StuckFailedAttempt_StaysStuck() =>
        Bucket("failed", [], stuck: true).Should().Be(TicketBucket.Stuck);
}
