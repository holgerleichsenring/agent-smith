using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

/// <summary>2026-11-17-5701a: a ticket carries its own specs as draft files under tickets/&lt;key&gt;/.</summary>
public sealed class TicketSpecDraftWriteTests
{
    private static readonly DraftScope Unbound = new(DraftWriteRig.Scope, Bound: false);

    [Fact]
    public void Write_SpecOfATicketOfTheDraft_IsStoredUnderItsTicket()
    {
        var draft = Put(Listed(), "tickets/order-api/order-state.yaml", DraftWriteRig.Spec("order-state", "The API returns the state."));

        draft.Tickets[0].Specs.Should().ContainSingle().Which.Goal.Should().Be("The API returns the state.");
        DraftFiles.Of(draft).Select(f => f.Path).Should().Equal(
            "tickets/order-api.yaml", "tickets/order-api/order-state.yaml", "tickets/order-ui.yaml");
    }

    [Fact]
    public void Write_TicketMissing_IsRefusedNamingTheTicketFile() =>
        Refusal(Listed(), "tickets/order-export/export-file.yaml", DraftWriteRig.Spec("export-file"))
            .Should().Contain("write draft:tickets/order-export.yaml first");

    [Theory]
    [InlineData("order-ui", "a ticket's key")]
    [InlineData("list", "the list's own review")]
    public void Write_LabelClashingWithTheList_IsRefused(string label, string reason) =>
        Refusal(Listed(), $"tickets/order-api/{label}.yaml", DraftWriteRig.Spec(label)).Should().Contain(reason);

    [Fact]
    public void Write_LabelHeldByAnotherTicketsSpec_IsRefused()
    {
        var draft = Put(Listed(), "tickets/order-api/order-state.yaml", DraftWriteRig.Spec("order-state"));

        Refusal(draft, "tickets/order-ui/order-state.yaml", DraftWriteRig.Spec("order-state"))
            .Should().Contain("draft:tickets/order-api/order-state.yaml holds 'order-state' already");
    }

    [Fact]
    public void Write_RequiresASpecTheTicketDoesNotHold_NamesTheFileToWrite() =>
        Refusal(Listed(), "tickets/order-api/order-state.yaml", DraftWriteRig.Spec("order-state", "A goal", "order-route"))
            .Should().Contain("write draft:tickets/order-api/order-route.yaml first");

    [Fact]
    public void Write_RequiresCycle_IsRefused()
    {
        var draft = Put(Put(Listed(), "tickets/order-api/order-state.yaml", DraftWriteRig.Spec("order-state")),
            "tickets/order-api/order-route.yaml", DraftWriteRig.Spec("order-route", "A goal", "order-state"));

        Refusal(draft, "tickets/order-api/order-state.yaml", DraftWriteRig.Spec("order-state", "A goal", "order-route"))
            .Should().Contain("cannot be put in an order");
    }

    [Fact]
    public void TicketRewrite_KeepsItsSpecs_TicketDelete_DropsThem()
    {
        var draft = Put(Listed(), "tickets/order-api/order-state.yaml", DraftWriteRig.Spec("order-state"));

        var rewritten = Put(draft, "tickets/order-api.yaml", "title: Orders API\nrequirement: Do it.\ndone: [it is done]\n");

        rewritten.Tickets[0].Specs.Should().ContainSingle();
        DraftFiles.Of(DraftWriteRig.Files().Delete(rewritten, "draft:tickets/order-api.yaml").Written()).Select(f => f.Path)
            .Should().Equal("tickets/order-ui.yaml");
    }

    [Fact]
    public void Delete_RequiredSpecRefused_DirectoryEmptiesTheTicket()
    {
        var draft = Put(Put(Listed(), "tickets/order-api/order-state.yaml", DraftWriteRig.Spec("order-state")),
            "tickets/order-api/order-route.yaml", DraftWriteRig.Spec("order-route", "A goal", "order-state"));

        DraftWriteRig.Files().Delete(draft, "draft:tickets/order-api/order-state.yaml").Refusal
            .Should().Contain("'order-route' require 'order-state'");
        DraftWriteRig.Files().Delete(draft, "draft:tickets/order-api/order-route.yaml").Written()
            .Tickets[0].Specs.Select(s => s.SpecId).Should().Equal("order-state");
        DraftWriteRig.Files().Delete(draft, "draft:tickets/order-api/").Written().Tickets[0].Specs.Should().BeEmpty();
        TicketSpecRemoval.Empty(draft, "order-export").Refusal.Should().Contain("no directory of this draft");
    }

    private static ProposalDraft Listed() =>
        new() { Tickets = [DraftWriteRig.Ticket("order-api"), DraftWriteRig.Ticket("order-ui")] };

    private static ProposalDraft Put(ProposalDraft draft, string path, string text) =>
        DraftWriteRig.Files().Put(draft, path, text, Unbound).Written();

    private static string? Refusal(ProposalDraft draft, string path, string text) =>
        DraftWriteRig.Files().Put(draft, path, text, Unbound).Refusal;
}
