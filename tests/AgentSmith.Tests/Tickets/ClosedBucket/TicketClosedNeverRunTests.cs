using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Tickets.ClosedBucket.ClosedFixtures;
using static AgentSmith.Tests.Tickets.NeverRun.NeverRunFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.ClosedBucket;

/// <summary>
/// 2026-10-15-114fb: a never-run filing with a close mark is closed; a ticket waiting for a closed ticket keeps
/// waiting and flags the entry, matched by (tracker connection, key) across projects; the graph shows the bucket.
/// </summary>
public sealed class TicketClosedNeverRunTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketList_ClosedNeverRunFiling_IsInTheClosedBucket()
    {
        await _h.AddAsync(Filing("59", "Orders: list screen"));
        await _h.CloseAsync(T.AddHours(1), ticket: "59");

        var row = (await _h.ListAsync(TicketBucket.Closed)).Tickets.Single();

        (row.TicketId, row.Kind, row.Since, row.Closed?.By).Should().Be(("59", TicketClosedKinds.ByHand, T.AddHours(1), "Ada"));
    }

    [Fact]
    public async Task TicketList_TicketWaitingForAClosedTicket_StaysBlockedAndFlagsTheEntryClosed()
    {
        await _h.AddAsync(Filing("59"), Filing("58"));
        await _h.RequiresAsync("59", "58", "41");
        await _h.CloseAsync(T.AddHours(1), ticket: "58");

        var row = (await _h.ListAsync()).Tickets.Single(t => t.TicketId == "59");

        row.Bucket.Should().Be(TicketBucket.Blocked);
        row.WaitsFor!.Select(w => (w.TicketId, w.Closed)).Should().Equal(("58", true), ("41", false));
    }

    [Fact]
    public async Task TicketList_WaitsForAClosedTicketOfAnotherProjectOnTheSameTracker_FlagsTheEntryClosed()
    {
        _h.Config.Projects["beta"] = new ResolvedProject
        {
            Name = "beta", Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
        };
        await _h.AddAsync(Filing("59"), Run("r-b", "58", status: "failed", project: "beta"), Step("r-b"));
        await _h.RequiresAsync("59", "58");
        await _h.CloseAsync(T.AddHours(1), ticket: "58", project: "beta");

        var rows = (await _h.List.ReadAsync(new TicketListRequest(ProjectScope.Everything, null, null, null), default)).Tickets;

        rows.Single(t => t.TicketId == "58").Bucket.Should().Be(TicketBucket.Closed);
        rows.Single(t => t.TicketId == "59").WaitsFor!.Single().Should().Match<TicketWaitsForView>(w => w.Project == "alpha" && w.Closed);
    }

    [Fact]
    public async Task TicketGraph_ClosedTicket_HasTheClosedNodeState()
    {
        await _h.AddAsync(Filing("58"), Filing("59"));
        await _h.RequiresAsync("59", "58");
        await _h.CloseAsync(T.AddHours(1), ticket: "58");

        var graph = await _h.Services.GetRequiredService<TicketGraphReader>().ReadAsync(ProjectScope.Single("alpha"), default);

        graph.Nodes.Single(n => n.TicketId == "58").State.Should().Be(TicketBucket.Closed);
    }
}
