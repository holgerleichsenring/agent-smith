using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.ClosedBucket.ClosedFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.ClosedBucket;

/// <summary>
/// 2026-10-15-114fc: the stored facts the rule reads — a head move or a reopen stamped after the close brings a
/// closed ticket back, one stamped before it does not; a failed attempt whose own check saw a person's tracker close
/// is closed (tracker) unless a pull request is still open.
/// </summary>
public sealed class TicketClosedStoredFactsTests : IDisposable
{
    private static readonly DateTimeOffset ClosedAt = T.AddHours(2);
    private static readonly TicketLiveState Live = new(
        new Dictionary<string, int>(), new HashSet<string>(), new Dictionary<string, PendingQuestionInfo>());

    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static PrReviewRecord Stamped(string state, DateTimeOffset? moved = null, DateTimeOffset? reopened = null)
    {
        var record = Record("1", state);
        (record.HeadSha, record.HeadMovedAtTicks, record.ReopenedAtTicks) = ("h1", moved?.UtcTicks, reopened?.UtcTicks);
        return record;
    }

    private static TicketPullRequestView Pr(string state) =>
        new("1", Url("1"), "api", state, TicketReviewDecisions.None, null, null, T.AddHours(1), null);

    [Fact]
    public async Task TicketClosedRule_HeadMovedAfterTheMark_IsNotInForce()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Stamped(PrReviewStates.Open, moved: ClosedAt.AddMinutes(1)));
        await _h.CloseAsync(ClosedAt);

        var row = await _h.RowAsync();

        (row.Bucket, row.PullRequests.Single().HeadMovedAt).Should().Be((TicketBucket.WaitingOnReview, (DateTimeOffset?)ClosedAt.AddMinutes(1)));
    }

    [Fact]
    public async Task TicketClosedRule_PullRequestReopenedAfterTheMark_IsNotInForce()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Stamped(PrReviewStates.Open, reopened: ClosedAt.AddMinutes(1)));
        await _h.CloseAsync(ClosedAt);

        var row = await _h.RowAsync();

        (row.Bucket, row.PullRequests.Single().ReopenedAt).Should().Be((TicketBucket.WaitingOnReview, (DateTimeOffset?)ClosedAt.AddMinutes(1)));
    }

    [Fact]
    public async Task TicketClosedRule_RefreshThatReadBeforeTheCloseWritesAfterIt_StaysInForce()
    {
        // The record was checked after the close (T+3h), but its stamps are the read's own times, both before the close.
        await _h.AddAsync(Run("r-1"), Step("r-1"),
            Stamped(PrReviewStates.Open, moved: ClosedAt.AddMinutes(-2), reopened: ClosedAt.AddMinutes(-1)));
        await _h.CloseAsync(ClosedAt);

        (await _h.RowAsync()).Bucket.Should().Be(TicketBucket.Closed);
    }

    [Fact]
    public void TicketBucketRule_FailedHeadClosedForWorkNoOpenPullRequest_IsClosedTracker()
    {
        var state = new TicketBucketRule().Of(Run("r-1", status: "failed"), Live, [Pr(PrReviewStates.Closed)], stuck: true, trackerClosed: true);

        (state.Bucket, state.Kind, state.LastOutcome).Should().Be((TicketBucket.Closed, TicketClosedKinds.Tracker, "failed"));
    }

    [Fact]
    public void TicketBucketRule_FailedHeadClosedForWorkWithAnOpenPullRequest_StaysWaitingOnReview() =>
        new TicketBucketRule().Of(Run("r-1", status: "failed"), Live, [Pr(PrReviewStates.Open)], trackerClosed: true)
            .Bucket.Should().Be(TicketBucket.WaitingOnReview);

    [Fact]
    public async Task TicketBucketRule_CheckOfAnOlderAttemptSaysClosed_IsNotClosed()
    {
        var newer = Run("r-2", status: "failed");
        newer.StartedAt = T.AddHours(1);
        await _h.AddAsync(Run("r-1", status: "failed"), Step("r-1"), newer, Step("r-2"),
            new TicketBetweenAttemptsCheck { RunId = "r-1", CheckedAt = T.AddHours(3), ClosedForWork = true });

        (await _h.RowAsync()).Bucket.Should().Be(TicketBucket.Failed, "only the newest attempt's own check decides");
    }
}
