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
}
