using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.Validation.TicketGraphFixtures;

namespace AgentSmith.Tests.Validation;

/// <summary>
/// 2026-10-14-dbcej: the `kind: tickets` block read untyped — a field of the wrong shape is refused naming
/// the ticket and the field, never read as empty, and nothing in the block's shape throws.
/// </summary>
public sealed class TicketGraphOutcomeParserTests
{
    private static string Error(string tickets) =>
        Parse(tickets).Should().BeOfType<OutcomeInvalid>().Subject.Error;

    // 2026-11-12-cd4cc: a list has no count.
    [Fact]
    public void TicketGraphOutcomeParser_TwentyOneTickets_AreAList() =>
        Parse(Item("orders-api") + string.Concat(Enumerable.Range(1, 20).Select(i => Item($"t{i}")))).Should().BeOfType<OutcomeResolved>();

    [Fact]
    public void TicketGraphOutcomeParser_UnknownTicketField_RefusedNamingTheTicket() =>
        Error(Item("orders-api", extra: "    spec: \"p1\"\n") + Item("orders-ui"))
            .Should().Contain("ticket 'orders-api'").And.Contain("unknown field 'spec'");

    [Fact]
    public void TicketGraphOutcomeParser_TicketNotAMapping_RefusedNamingItsPosition() =>
        Error(Item("orders-api") + "  - \"just a sentence\"\n").Should().Contain("ticket #2 is not a mapping");

    [Fact]
    public void TicketGraphOutcomeParser_DoneItemWithUnquotedColon_RefusedNamingTheFieldAndQuoting() =>
        Error("  - key: \"orders-api\"\n    title: \"t\"\n    requirement: \"r\"\n    done:\n      - GET /orders returns: 200\n"
                + Item("orders-ui"))
            .Should().Contain("ticket 'orders-api'").And.Contain("'done'").And.Contain("quote the item");

    [Fact]
    public void TicketGraphOutcomeParser_TitleAsANestedMapping_RefusedAsNotAString() =>
        Error("  - key: \"orders-api\"\n    title:\n      a: b\n    requirement: \"r\"\n    done: [\"d\"]\n" + Item("orders-ui"))
            .Should().Contain("ticket 'orders-api'").And.Contain("'title' is not a string").And.Contain("quote the item");

    [Fact]
    public void TicketGraphOutcomeParser_RequiresAsAScalar_RefusedNotDropped() =>
        Error(Item("order-api") + Item("orders-ui", requires: "order-api"))
            .Should().Contain("ticket 'orders-ui'").And.Contain("'requires' is not a list")
            .And.Contain("write it as a list: [order-api]");

    [Fact]
    public void TicketGraphOutcomeParser_DoneAsAScalar_RefusedAsNotAList() =>
        Error("  - key: \"orders-api\"\n    title: \"t\"\n    requirement: \"r\"\n    done: \"it works\"\n" + Item("orders-ui"))
            .Should().Contain("ticket 'orders-api'").And.Contain("'done' is not a list");

    [Fact]
    public void TicketGraphOutcomeParser_RootWithoutRequiresKey_Accepted()
    {
        var graph = Parse("  - key: \"orders-api\"\n    title: \"t\"\n    requirement: \"r\"\n    done: [\"d\"]\n" + Item("orders-ui", "[\"orders-api\"]"))
            .Should().BeOfType<OutcomeResolved>().Which.Proposal.Should().BeOfType<TicketGraphOutcome>().Subject;

        graph.Tickets[0].Requires.Should().BeEmpty();
        graph.Tickets[1].Requires.Should().Equal("orders-api");
    }

    [Fact]
    public void TicketGraphOutcomeParser_NullRequiresAndRepos_ReadAsEmpty()
    {
        var graph = Parse(Item("orders-api", requires: "~", extra: "    repos:\n") + Item("orders-ui", requires: "null"))
            .Should().BeOfType<OutcomeResolved>().Which.Proposal.Should().BeOfType<TicketGraphOutcome>().Subject;

        graph.Tickets.Should().AllSatisfy(t =>
        {
            t.Requires.Should().BeEmpty();
            t.Repos.Should().BeEmpty();
        });
    }
}
