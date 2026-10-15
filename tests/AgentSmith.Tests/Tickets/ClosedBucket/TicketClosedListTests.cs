using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.ClosedBucket.ClosedFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.ClosedBucket;

/// <summary>
/// 2026-10-15-114fb: a run-bearing ticket closed by hand lists under Closed with who, when and why, comes back on
/// a person's later review, costs no background tracker read, and is counted beside the failed and PR-closed ones.
/// </summary>
public sealed class TicketClosedListTests : IDisposable
{
    private static readonly DateTimeOffset ClosedAt = T.AddHours(2);
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketList_ClosedTicket_IsInTheClosedBucketWithWhoWhenAndNote()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Open));
        await _h.CloseAsync(ClosedAt);

        var row = await _h.RowAsync();

        row.Bucket.Should().Be(TicketBucket.Closed);
        row.Closed.Should().BeEquivalentTo(new TicketClosedView(ClosedAt, "Ada", "duplicate of 41", [], false));
    }

    [Fact]
    public async Task TicketList_ClosedTicketWithALaterPersonReview_IsBackInWaitingOnReview()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Open),
            Entry("1", "r1", ClosedAt.AddMinutes(10), decision: PrReviewDecisions.ChangesRequested));
        await _h.CloseAsync(ClosedAt);

        var row = await _h.RowAsync();

        (row.Bucket, row.Kind, row.Closed?.At).Should().Be((TicketBucket.WaitingOnReview, null, ClosedAt));
        row.PullRequests.Single().PersonActivityAt.Should().Be(ClosedAt.AddMinutes(10));
    }

    [Fact]
    public async Task TicketList_FailedTicketClosedByHand_IsNotOfferedToTheCheck()
    {
        await _h.AddAsync(Run("r-1", status: "failed"), Step("r-1"), Run("r-2", "43", status: "failed"), Step("r-2"));
        await _h.CloseAsync(ClosedAt);

        await _h.List.ReadAsync(new TicketListRequest(ProjectScope.Single("alpha"), null, null, null), default);

        _h.Kicks.Reader.TryRead(out var kick).Should().BeTrue();
        (kick!.TicketId, _h.Kicks.Reader.Count).Should().Be(("43", 0));
    }

    [Fact]
    public async Task TicketList_Counts_CountClosedFailedAndPrClosedAndKeepIdleForTheRest()
    {
        await _h.AddAsync(Run("r-1", "41", status: "failed"), Step("r-1"), Run("r-2", "42", status: "failed"), Step("r-2"),
            Run("r-3", "43"), Step("r-3"), Record("3", PrReviewStates.Closed, ticket: "43"),
            Run("r-4", "44", status: "cancelled"), Step("r-4"), Run("r-5", "45"), Step("r-5"));
        await _h.CloseAsync(ClosedAt, ticket: "41");

        var counts = (await _h.List.ReadAsync(new TicketListRequest(ProjectScope.Single("alpha"), null, null, null), default)).Counts;

        (counts.Closed, counts.Failed, counts.PrClosed, counts.Idle).Should().Be((1, 1, 1, 2));
    }

    [Fact]
    public async Task TicketDetail_ClosedTicket_HeaderCarriesTheMark()
    {
        await _h.AddAsync(Run("r-1", status: "failed"), Step("r-1"));
        await _h.CloseAsync(ClosedAt);

        var summary = (await _h.Detail.ReadAsync("alpha", "42", default))!.Summary;

        (summary.Bucket, summary.Kind, summary.Closed?.By).Should().Be((TicketBucket.Closed, TicketClosedKinds.ByHand, "Ada"));
    }
}
