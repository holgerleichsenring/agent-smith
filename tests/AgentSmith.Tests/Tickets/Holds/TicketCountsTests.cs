using AgentSmith.Application.Services.Holds;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Holds;
using AgentSmith.Server.Services.Tickets.Text;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-31dcg: Your move counts a person's holds, and an admin's apart.</summary>
public sealed class TicketCountsTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-23T09:00:00Z");

    private static TicketHold Hold(string addressee, string @case, DateTimeOffset? since = null) => new(addressee, @case, null, since);

    private static TicketSummaryView Row(string id, string bucket, TicketHold? hold, DateTimeOffset? since = null) =>
        new("alpha", id, null, null, null, bucket, null, since ?? T, 1, 0m, $"r-{id}", "success", null, null, null, 0, 0, [], [], Hold: hold);

    [Fact]
    public void Counts_AMergeHoldIsYourMove_ANobodyHoldIsNot_AnAdminHoldCountsApart()
    {
        var counts = new TicketCounts(TimeProvider.System).Of([
            Row("1", TicketBucket.NeedsYou, Hold(TicketHoldAddressees.Merge, TicketHoldCases.MergeNext), T.AddHours(2)),
            Row("2", TicketBucket.Working, Hold(TicketHoldAddressees.Nobody, TicketHoldCases.MergeAfter)),
            Row("3", TicketBucket.Stuck, Hold(TicketHoldAddressees.Admin, TicketHoldCases.ImageUntrusted)),
            Row("4", TicketBucket.NeedsYou, Hold(TicketHoldAddressees.Decide, TicketHoldCases.Failed), T.AddHours(1)),
        ], 0m, false);

        (counts.NeedsYou, counts.AdminHolds, counts.Stuck, counts.Working, counts.Queued).Should().Be((2, 1, 1, 1, 0),
            "a pull request that waits is working, and neither running nor queued");
        counts.OldestNeedsYouSince.Should().Be(T.AddHours(1));
    }
}
