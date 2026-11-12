using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.Validation.TicketGraphFixtures;

namespace AgentSmith.Tests.Validation;

/// <summary>2026-10-14-dbcej: the rules a list of tickets is filed under, each reason naming the ticket and the rule.</summary>
public sealed class TicketGraphCheckerTests
{
    private static string? Check(params GraphTicket[] tickets) => Checker().Check(new TicketGraphOutcome(tickets), RepoScope);

    [Fact]
    public void TicketGraphChecker_DuplicateKey_Refused() =>
        Check(Ticket("orders-api"), Ticket("orders-api")).Should().Contain("'orders-api' is named twice");

    [Fact]
    public void TicketGraphChecker_KeyWithUpperCase_Refused() =>
        Check(Ticket("Orders-api"), Ticket("orders-ui")).Should().Contain("ticket #1 has the key 'Orders-api'");

    [Fact]
    public void TicketGraphChecker_KeyOverFortyOneCharacters_Refused()
    {
        Check(Ticket("a" + new string('b', 40)), Ticket("orders-ui")).Should().BeNull();
        Check(Ticket("a" + new string('b', 41)), Ticket("orders-ui")).Should().Contain("1 to 40");
    }

    [Fact]
    public void TicketGraphChecker_MissingRequirement_RefusedNamingTheTicket() =>
        Check(Ticket("orders-api") with { Requirement = "" }, Ticket("orders-ui"))
            .Should().Contain("ticket 'orders-api' has no 'requirement'");

    [Fact]
    public void TicketGraphChecker_NoDoneItem_RefusedNamingTheTicket() =>
        Check(Ticket("orders-api") with { Done = [] }, Ticket("orders-ui")).Should().Contain("ticket 'orders-api' has no 'done' item");

    // 2026-11-12-cd4cd: no length or count bound on a ticket's fields.
    [Fact]
    public void TicketGraphChecker_LongFields_AreAccepted() =>
        Check(Ticket("orders-api") with
        {
            Title = new string('t', 300), Requirement = new string('r', 5000),
            Done = [.. Enumerable.Range(1, 12).Select(i => new string('d', 400))],
        }, Ticket("orders-ui")).Should().BeNull();

    [Fact]
    public void GraphTicketLines_MultiLineTitleAndDoneItem_AreFoldedToOneLine()
    {
        var ticket = GraphTicketLines.Normalized(Ticket("orders-api") with { Title = "first\nsecond", Done = ["a\n  b"] });

        ticket.Title.Should().Be("first second");
        ticket.Done.Should().Equal("a b");
    }

    [Fact]
    public void TicketGraphChecker_RequiresUnknownSibling_RefusedNamingIt() =>
        Check(Ticket("orders-api"), Ticket("orders-ui", "orders-apx"))
            .Should().Contain("ticket 'orders-ui' requires 'orders-apx', which is no ticket of this list");

    [Fact]
    public void TicketGraphChecker_RequiresItself_Refused() =>
        Check(Ticket("orders-api"), Ticket("orders-ui", "orders-ui")).Should().Contain("ticket 'orders-ui' requires itself");

    [Fact]
    public void TicketGraphChecker_DuplicateRequires_Refused() =>
        Check(Ticket("orders-api"), Ticket("orders-ui", "ticket:41", "ticket:#41")).Should().Contain("requires 'ticket:#41' twice");

    [Fact]
    public void TicketGraphChecker_RequiresExistingTicket_Accepted() =>
        Check(Ticket("orders-api", "ticket:19438"), Ticket("orders-ui", "orders-api", "ticket:#41")).Should().BeNull();

    [Fact]
    public void TicketGraphChecker_EmptyExistingTicketId_Refused() =>
        Check(Ticket("orders-api", "ticket:"), Ticket("orders-ui")).Should().Contain("requires 'ticket:' with no id");

    [Fact]
    public void TicketGraphChecker_ExistingTicketIdOnlyAHash_Refused() =>
        Check(Ticket("orders-api", "ticket:#"), Ticket("orders-ui")).Should().Contain("requires 'ticket:#' with no id");

    [Fact]
    public void TicketGraphChecker_RepoOutsideTheScope_Refused() =>
        Check(Ticket("orders-api") with { Repos = ["api"] }, Ticket("orders-ui") with { Repos = ["mobile"] })
            .Should().Contain("ticket 'orders-ui' names the repository 'mobile'").And.Contain("api, web");

    [Fact]
    public void TicketGraphChecker_Cycle_RefusedNamingTheWaitingKeys() =>
        Check(Ticket("root"), Ticket("orders-api", "orders-ui"), Ticket("orders-ui", "orders-api"))
            .Should().Contain("'orders-api', 'orders-ui' each wait on another ticket");

    [Fact]
    public void TicketGraphOrderer_ForwardEdge_OrdersRequiredFirst_TiesKeepProposalOrder()
    {
        var (tickets, error) = new TicketGraphOrderer().Order(
            [Ticket("ui", "api"), Ticket("docs"), Ticket("api"), Ticket("report")]);

        error.Should().BeNull();
        tickets.Select(t => t.Key).Should().Equal("docs", "api", "ui", "report");
    }

    [Fact]
    public void TicketGraphOrderer_ExistingTicketRequirement_DoesNotConstrainOrder()
    {
        var (tickets, error) = new TicketGraphOrderer().Order([Ticket("ui", "ticket:41"), Ticket("api", "ticket:#7")]);

        error.Should().BeNull();
        tickets.Select(t => t.Key).Should().Equal("ui", "api");
    }

    [Fact]
    public void GraphTicketRequire_ExistingId_StripsThePrefixAndRejectsOthers()
    {
        GraphTicketRequire.ExistingId("ticket:19438").Should().Be("19438");
        GraphTicketRequire.ExistingId("ticket:PROJ-12").Should().Be("PROJ-12");
        GraphTicketRequire.ExistingId("orders-api").Should().BeNull();
        GraphTicketRequire.ExistingId("Ticket:41").Should().BeNull();
    }

    [Fact]
    public void GraphTicketRequire_ExistingId_HashPrefixedId_IsTheBareId()
    {
        GraphTicketRequire.ExistingId("ticket:#41").Should().Be("41");
        GraphTicketRequire.ExistingId("ticket:##41").Should().Be("#41");
    }
}
