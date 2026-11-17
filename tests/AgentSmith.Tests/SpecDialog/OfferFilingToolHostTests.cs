using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-17-5701a: a list of tickets is offered for filing once every ticket carries a spec.</summary>
public sealed class OfferFilingToolHostTests
{
    private static readonly SpecDraft Spec = new("order-state", "The API returns the state.", "spec: order-state", []);

    [Fact]
    public async Task OfferFiling_ListWithASpecLessTicket_IsRefusedNamingIt()
    {
        var slot = Listed(new GraphTicket("order-api", "A", "r", ["d"], [], []) { Specs = [Spec] },
            new GraphTicket("order-ui", "U", "r", ["d"], ["order-api"], []));

        var answer = await OfferAsync(slot);

        answer.Should().StartWith("Not offered").And.Contain("'order-ui'").And.Contain("draft:tickets/order-ui/<label>.yaml")
            .And.NotContain("'order-api'");
        slot.Offer.Should().BeNull();
    }

    [Fact]
    public async Task OfferFiling_EveryTicketWithASpec_IsOffered()
    {
        var slot = Listed(new GraphTicket("order-api", "A", "r", ["d"], [], []) { Specs = [Spec] });

        (await OfferAsync(slot)).Should().StartWith("Offered");
    }

    private static SpecDialogReplySlot Listed(params GraphTicket[] tickets)
    {
        var slot = new SpecDialogReplySlot();
        slot.Proposal.Seed(new ProposalDraft { Tickets = tickets }, changed: true);
        return slot;
    }

    private static async Task<string> OfferAsync(SpecDialogReplySlot slot) =>
        (await new OfferFilingToolHost(slot).GetTools(null, null).Single().InvokeAsync(
            new AIFunctionArguments { ["line"] = "Files the list." }))!.ToString()!;
}
