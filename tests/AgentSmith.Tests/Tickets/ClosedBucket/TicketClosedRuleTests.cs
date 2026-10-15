using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.ClosedBucket.ClosedFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.ClosedBucket;

/// <summary>
/// 2026-10-15-114fb: a close mark is in force until a later attempt or a person's pull-request entry past the skew
/// margin; ours, a bot's and a host system note without a decision never undo it, and neither does a later read.
/// </summary>
public sealed class TicketClosedRuleTests : IDisposable
{
    private static readonly DateTimeOffset ClosedAt = T.AddHours(2);
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketClosedRule_MarkAfterTheNewestAttempt_IsInForce()
    {
        await _h.AddAsync(Run("r-1", status: "failed"), Step("r-1"));
        await _h.CloseAsync(ClosedAt);

        (await _h.RowAsync()).Should().Match<TicketSummaryView>(r =>
            r.Bucket == TicketBucket.Closed && r.Kind == TicketClosedKinds.ByHand && r.Since == ClosedAt && r.LastOutcome == "failed");
    }

    [Fact]
    public async Task TicketClosedRule_AttemptStartedAfterTheMark_IsNotInForce()
    {
        await _h.AddAsync(Run("r-1", status: "failed"), Step("r-1"));
        await _h.CloseAsync(T.AddHours(-1));

        var row = await _h.RowAsync();

        (row.Bucket, row.Closed!.At).Should().Be((TicketBucket.Failed, T.AddHours(-1)), "the mark stays stored while it is not in force");
    }

    [Fact]
    public async Task TicketClosedRule_PersonCommentOnThePullRequestAfterTheMark_IsNotInForce()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Closed), Entry("1", "c1", ClosedAt.AddMinutes(5)));
        await _h.CloseAsync(ClosedAt);

        (await _h.RowAsync()).Bucket.Should().Be(TicketBucket.PrClosed);
    }

    [Fact]
    public async Task TicketClosedRule_OurOwnOrABotsOrASystemNoteAfterTheMark_StaysInForce()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Closed),
            Entry("1", "c1", ClosedAt.AddMinutes(1), ours: true), Entry("1", "c2", ClosedAt.AddMinutes(2), bot: true),
            Entry("1", "c3", ClosedAt.AddMinutes(3), system: true));
        await _h.CloseAsync(ClosedAt);

        (await _h.RowAsync()).Bucket.Should().Be(TicketBucket.Closed);
    }

    [Fact]
    public async Task TicketClosedRule_ApprovalWrittenAsASystemNoteAfterTheMark_IsNotInForce()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Open),
            Entry("1", "c1", ClosedAt.AddMinutes(1), system: true, decision: PrReviewDecisions.Approved));
        await _h.CloseAsync(ClosedAt);

        (await _h.RowAsync()).Bucket.Should().Be(TicketBucket.WaitingOnReview, "a decision a host wrote as a system note is a person's review");
    }

    [Fact]
    public async Task TicketClosedRule_CommentWithinTheSkewMargin_StaysInForce()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Open), Entry("1", "c1", ClosedAt.AddSeconds(3)));
        await _h.CloseAsync(ClosedAt);

        (await _h.RowAsync()).Bucket.Should().Be(TicketBucket.Closed);
    }

    [Fact]
    public async Task TicketClosedRule_ClosedPullRequestReReadFailedAfterTheMark_StaysInForce()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Closed));
        await _h.CloseAsync(T.AddHours(1).AddMinutes(30));

        var row = await _h.RowAsync();

        (row.Bucket, row.PullRequests.Single().State).Should().Be((TicketBucket.Closed, PrReviewStates.Closed),
            "the record was checked after the mark, which a failed read also does");
    }

    [Fact]
    public async Task TicketClosedRule_HideOnlyWithTheOpenPullRequestUntouched_StaysInForce()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Open));
        await _h.CloseAsync(ClosedAt);

        var row = await _h.RowAsync();

        (row.Bucket, row.PullRequests.Single().State).Should().Be((TicketBucket.Closed, PrReviewStates.Open));
    }
}
