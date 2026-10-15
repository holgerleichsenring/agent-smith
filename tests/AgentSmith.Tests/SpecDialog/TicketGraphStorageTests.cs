using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.Conversation;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-10-14-dbcej: a list-of-tickets proposal on the stored row, the kept turn and the bound ticket page.</summary>
public sealed class TicketGraphStorageTests
{
    private static readonly TicketGraphOutcome Graph = new TicketGraphOutcome(
    [
        new GraphTicket("orders-api", "Orders API", "The API returns the state.", ["GET answers 200", "PATCH moves it"], ["ticket:#41"], ["api"]),
        new GraphTicket("orders-ui", "Orders UI", "The page shows the state.", ["the column shows it"], ["orders-api"], []),
    ])
    {
        Templates = [new TemplateProvenance("templates/web", "templates", "abc123", true)],
        Findings = [new ProposalFinding("orders-api", "not reviewed yet", "the share ran out")],
        ReviewRounds = [new ProposalReviewRound("orders-api", 1, "BUILD", ["BUILD"], [], [], [])],
        NotReviewed = ["orders-ui"],
    };

    [Fact]
    public void OutcomeProposalJson_TicketGraph_RoundTripsEveryTicketFieldAndTheReviewFields()
    {
        var json = OutcomeProposalJson.Write(Graph);
        var back = OutcomeProposalJson.Read(json).Should().BeOfType<TicketGraphOutcome>().Subject;

        json.Should().Contain("\"kind\":\"tickets\"");
        back.Tickets.Should().BeEquivalentTo(Graph.Tickets, o => o.WithStrictOrdering());
        back.Templates.Should().BeEquivalentTo(Graph.Templates);
        back.Findings.Should().BeEquivalentTo(Graph.Findings);
        back.ReviewRounds.Should().BeEquivalentTo(Graph.ReviewRounds);
        back.NotReviewed.Should().Equal("orders-ui");
    }

    [Fact]
    public void SpecDialogTurnResult_TicketGraph_KeptAsTicketGraphProposal()
    {
        SpecDialogTurnResult.KindOf(Graph).Should().Be(SpecDialogTurnKind.TicketGraphProposal);
        ((int)SpecDialogTurnKind.TicketGraphProposal).Should().Be(8);
    }

    [Fact]
    public void TicketTranscriptReader_TicketGraphProposal_ShownAsTickets()
    {
        var reader = new TicketTranscriptReader(Mock.Of<IServiceScopeFactory>(), NullLogger<TicketTranscriptReader>.Instance);

        var view = reader.Proposal(TicketConversationFixtures.Session(Graph))!;

        view.Kind.Should().Be("tickets");
        view.Drafts.Should().BeEmpty();
        view.NotReviewed.Should().Equal("orders-ui");
    }
}
