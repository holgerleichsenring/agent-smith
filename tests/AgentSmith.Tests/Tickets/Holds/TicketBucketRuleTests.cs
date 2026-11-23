using AgentSmith.Application.Services.Holds;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Holds;
using AgentSmith.Server.Services.Tickets.Text;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-31dcg: the bucket takes the hold — a person's hold is needs_you, an admin's stuck, nobody's leaves the state.</summary>
public sealed class TicketBucketRuleTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-23T09:00:00Z");
    private static readonly TicketBucketState Review = new(TicketBucket.WaitingOnReview, null, T, null, null, null, null);

    private static TicketHold Hold(string addressee, string @case, DateTimeOffset? since = null) => new(addressee, @case, null, since);

    [Fact]
    public void Held_AMergeHold_IsNeedsYouSinceTheHold()
    {
        var state = new TicketBucketRule().Held(Review, Hold(TicketHoldAddressees.Merge, TicketHoldCases.MergeNext, T.AddHours(1)));

        (state.Bucket, state.Kind, state.Since).Should().Be((TicketBucket.NeedsYou, (string?)null, (DateTimeOffset?)T.AddHours(1)));
    }

    [Fact]
    public void Held_ANobodyHold_LeavesTheState_AnAdminHold_IsStuck()
    {
        var rule = new TicketBucketRule();

        rule.Held(Review, Hold(TicketHoldAddressees.Nobody, TicketHoldCases.MergeAfter)).Should().Be(Review);
        rule.Held(Review, null).Should().Be(Review);
        rule.Held(Review with { Bucket = TicketBucket.Failed }, Hold(TicketHoldAddressees.Admin, TicketHoldCases.Preflight))
            .Bucket.Should().Be(TicketBucket.Stuck);
    }

    [Fact]
    public void Held_AHandbackWithoutARecordedTime_HasNoSince_AQuestionCarriesItsKind()
    {
        var rule = new TicketBucketRule();
        var working = Review with { Bucket = TicketBucket.Working };

        rule.Held(working, Hold(TicketHoldAddressees.Decide, TicketHoldCases.Handback)).Should()
            .Match<TicketBucketState>(s => s.Bucket == TicketBucket.NeedsYou && s.Kind == TicketNeedsYouKind.Handback && s.Since == null);
        rule.Held(working, Hold(TicketHoldAddressees.Decide, TicketHoldCases.Question, T)).Kind.Should().Be(TicketNeedsYouKind.Question);
    }
}
