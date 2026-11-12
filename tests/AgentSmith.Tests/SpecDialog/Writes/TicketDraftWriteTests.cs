using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

public sealed class TicketDraftWriteTests
{
    [Fact]
    public void WriteTicket_RequiresUnknownKey_IsRefused()
    {
        var write = DraftWriteRig.Tickets().Write(ProposalDraft.Empty, DraftWriteRig.Ticket("api-filter", "api-schema"), DraftWriteRig.Scope);

        write.Next.Should().BeNull();
        write.Refusal.Should().Contain("requires 'api-schema', which is no ticket of this list");
    }

    [Fact]
    public void WriteTicket_SameKey_ReplacesTheTicketInPlace()
    {
        var tickets = DraftWriteRig.Tickets();
        var draft = tickets.Write(ProposalDraft.Empty, DraftWriteRig.Ticket("api-schema"), DraftWriteRig.Scope).Written();
        draft = tickets.Write(draft, DraftWriteRig.Ticket("api-filter", "api-schema"), DraftWriteRig.Scope).Written();

        var rewritten = tickets.Write(draft, DraftWriteRig.Ticket("api-schema") with { Title = "New" }, DraftWriteRig.Scope).Written();

        rewritten.Tickets.Select(t => t.Key).Should().Equal("api-schema", "api-filter");
        rewritten.Tickets[0].Title.Should().Be("New");
    }

    [Fact]
    public void WriteTicket_RewriteThatClosesACycle_IsRefused()
    {
        var tickets = DraftWriteRig.Tickets();
        var draft = tickets.Write(ProposalDraft.Empty, DraftWriteRig.Ticket("a-one"), DraftWriteRig.Scope).Written();
        draft = tickets.Write(draft, DraftWriteRig.Ticket("b-two", "a-one"), DraftWriteRig.Scope).Written();

        var write = tickets.Write(draft, DraftWriteRig.Ticket("a-one", "b-two"), DraftWriteRig.Scope);

        write.Refusal.Should().Contain("cannot be put in an order");
    }

    [Fact]
    public void WriteTicket_DraftHoldsASpec_IsRefusedAsAnotherKind()
    {
        var draft = DraftWriteRig.Specs().Write(ProposalDraft.Empty, DraftWriteRig.Spec("p9001"), false, []).Written();

        DraftWriteRig.Tickets().Write(draft, DraftWriteRig.Ticket("a-one"), DraftWriteRig.Scope).Refusal
            .Should().Contain("a proposal is one kind");
    }

    [Fact]
    public void WriteTicket_RepositoryOutsideScope_IsRefused()
    {
        var ticket = DraftWriteRig.Ticket("a-one") with { Repos = ["web"] };

        DraftWriteRig.Tickets().Write(ProposalDraft.Empty, ticket, ["api", "db"]).Refusal
            .Should().Contain("names the repository 'web'");
    }
}
