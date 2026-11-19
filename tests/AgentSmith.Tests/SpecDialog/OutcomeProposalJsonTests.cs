using AgentSmith.Contracts.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-17-5701a: a ticket's specs ride the stored proposal; a row stored before them reads back empty.</summary>
public sealed class OutcomeProposalJsonTests
{
    [Fact]
    public void Read_StoredListWithoutSpecs_ReadsBackWithEmptySpecs()
    {
        const string json = """{"kind":"tickets","tickets":[{"key":"order-api","title":"Orders API","requirement":"The API.","done":["it answers"],"requires":[],"repos":[]}]}""";

        var back = OutcomeProposalJson.Read(json).Should().BeOfType<TicketGraphOutcome>().Subject;

        back.Tickets.Should().ContainSingle().Which.Specs.Should().BeEmpty();
    }

    [Fact]
    public void Write_ListWithSpecs_RoundTripsThem()
    {
        var spec = new SpecDraft("order-state", "The API returns the state.", "spec: order-state", []) { Repos = ["api"] };
        var list = new TicketGraphOutcome([new GraphTicket("order-api", "Orders API", "The API.", ["it answers"], [], []) { Specs = [spec] }]);

        var back = (TicketGraphOutcome)OutcomeProposalJson.Read(OutcomeProposalJson.Write(list));

        back.Tickets.Single().Specs.Should().ContainSingle().Which.Should().BeEquivalentTo(spec);
    }

    // 2026-11-19-6b0bd: the under-review state is retired; a row stored while it existed still reads, and is not written back.
    [Fact]
    public void Read_StoredRowWithUnderReview_ReadsAndWritesWithoutIt()
    {
        var stored = OutcomeProposalJson.Write(new BugOutcome(new BugTicketDraft("It vanished", "Gone.", null)));
        var json = stored[..^1] + ",\"underReview\":true}";

        var back = OutcomeProposalJson.Read(json).Should().BeOfType<BugOutcome>().Subject;

        back.Ticket.Title.Should().Be("It vanished");
        OutcomeProposalJson.Write(back).Should().Be(stored);
    }
}
