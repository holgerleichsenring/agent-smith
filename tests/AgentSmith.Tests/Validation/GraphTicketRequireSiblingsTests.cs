using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.Validation.TicketGraphFixtures;

namespace AgentSmith.Tests.Validation;

/// <summary>2026-11-19-46bd: a ticket:&lt;key&gt; naming a ticket of the same list is that sibling.</summary>
public sealed class GraphTicketRequireSiblingsTests
{
    [Fact]
    public void Siblings_PrefixedSiblingKey_BecomesTheBareKey()
    {
        var list = GraphTicketRequire.Siblings(
            [Ticket("foundation"), Ticket("core", "ticket:foundation"), Ticket("delivery", "ticket:#core", "foundation")]);

        list.Select(t => t.Requires).Should().BeEquivalentTo(
            new[] { Array.Empty<string>(), ["foundation"], new[] { "core", "foundation" } },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Siblings_PrefixedIdOfNoSibling_StaysATrackerId()
    {
        var list = GraphTicketRequire.Siblings(
            [Ticket("core", "ticket:41", "ticket:#41", "ticket:PROJ-7"), Ticket("ui", "ticket:Core")]);

        list[0].Requires.Should().Equal("ticket:41", "ticket:#41", "ticket:PROJ-7");
        list[1].Requires.Should().ContainSingle().Which.Should().Be("ticket:Core", "a key is matched ordinal");
    }

    [Fact]
    public void Siblings_NoneNamed_ReturnsTheListAndItsTickets()
    {
        IReadOnlyList<GraphTicket> tickets = [Ticket("core", "ticket:41"), Ticket("ui", "core")];

        GraphTicketRequire.Siblings(tickets).Should().BeSameAs(tickets);
    }

    [Fact]
    public void TicketGraphOutcome_PrefixedSibling_ReadAsTheSiblingByEveryReader()
    {
        var outcome = new TicketGraphOutcome([Ticket("core"), Ticket("ui", "ticket:core")]);

        outcome.Tickets[1].Requires.Should().Equal("core");
        Checker().Check(outcome, RepoScope).Should().BeNull();
    }
}
