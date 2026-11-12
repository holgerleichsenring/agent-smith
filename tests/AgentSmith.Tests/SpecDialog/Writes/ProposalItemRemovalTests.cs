using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

public sealed class ProposalItemRemovalTests
{
    [Fact]
    public void Remove_TicketAnotherRequires_IsRefused()
    {
        var draft = new ProposalDraft { Tickets = [DraftWriteRig.Ticket("a-one"), DraftWriteRig.Ticket("b-two", "a-one")] };

        ProposalItemRemoval.Remove(draft, "a-one").Refusal.Should().Contain("'b-two' require 'a-one'");
    }

    [Fact]
    public void Remove_UnrequiredTicket_TakesItOut()
    {
        var draft = new ProposalDraft { Tickets = [DraftWriteRig.Ticket("a-one"), DraftWriteRig.Ticket("b-two", "a-one")] };

        ProposalItemRemoval.Remove(draft, "b-two").Written().Tickets.Select(t => t.Key).Should().Equal("a-one");
    }

    [Fact]
    public void Remove_ParentWithChildren_IsRefused()
    {
        var draft = new ProposalDraft
        {
            Specs = [Spec("p9000", true), Spec("p9000a", false), Spec("p9000b", false)],
        };

        ProposalItemRemoval.Remove(draft, "p9000").Refusal.Should().Contain("is the parent of 2 children");
    }

    [Fact]
    public void Remove_UnknownHandle_NamesWhatTheDraftHolds()
    {
        ProposalItemRemoval.Remove(new ProposalDraft { Bug = new BugTicketDraft("t", "d", null) }, "x").Refusal
            .Should().Contain("it holds bug");
    }

    [Fact]
    public void Remove_Bug_EmptiesTheDraft()
    {
        ProposalItemRemoval.Remove(new ProposalDraft { Bug = new BugTicketDraft("t", "d", null) }, "bug").Written()
            .IsEmpty.Should().BeTrue();
    }

    private static DraftSpec Spec(string id, bool parent) => new(new SpecDraft(id, "g", $"spec: {id}", []), parent);
}
