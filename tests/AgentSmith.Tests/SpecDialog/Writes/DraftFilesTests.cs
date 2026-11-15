using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

/// <summary>2026-11-15-06fc: a draft as files is a view of the typed draft, and each file reads back to its item.</summary>
public sealed class DraftFilesTests
{
    [Fact]
    public void Of_Epic_ListsTheParentFirstThenTheSpecsAndTheContract()
    {
        var contract = new SeriesContract([], [], [new SeriesContractArea("p1", ["a.cs"])], []);
        var draft = new ProposalDraft { Specs = [Spec("p1", false), Spec("p0", true)], Contract = contract };

        DraftFiles.Of(draft).Select(f => f.Path).Should().Equal("epic/p0.yaml", "specs/p1.yaml", "contract.yaml");
        DraftFiles.Find(draft, "specs/p1.yaml")!.Text.Should().Be("spec: p1\n");
    }

    [Fact]
    public void Ticket_RendersAndReadsBack()
    {
        var ticket = new GraphTicket("b-two", "Filter: by status", "Filter the list.\nKeep the order: newest first.",
            ["WHEN a status is picked THEN only those show", "the filter is kept"], ["a-one", "ticket:41"], ["api"]);

        DraftTicketText.Read("b-two", DraftTicketText.Render(ticket)).Ticket.Should().BeEquivalentTo(ticket);
    }

    [Fact]
    public void Bug_RendersAndReadsBack()
    {
        var bug = new BugTicketDraft("Fix: the null", "It throws\nwhen empty.", "- it no longer throws\n- a test covers it");

        var draft = DraftWriteRig.Files().Put(ProposalDraft.Empty, "bug.yaml", DraftBugText.Render(bug),
            new DraftScope([], false)).Written();

        draft.Bug.Should().Be(bug);
    }

    [Fact]
    public void Contract_RendersAndReadsBack()
    {
        var contract = new SeriesContract(
            [new SeriesContractName("OrderPlaced", "event", 7, "p1", ["p2"])],
            [new SeriesContractDatum("order status", "Orders table", "p1")],
            [new SeriesContractArea("p2", ["src/api/", "src/x.cs"])],
            [new SeriesContractBudget("src/x.cs", "p2", 120)]);
        var (json, error) = DraftContractText.Read(DraftContractText.Render(contract));

        error.Should().BeNull();
        var read = TestSupport.SeriesContracts.Reader().FromDraft((JsonElement)json!,
            [new SpecDraft("p1", "g", "spec: p1", []), new SpecDraft("p2", "g", "spec: p2", [])]);

        read.Problem.Should().BeNull();
        read.Contract.Should().BeEquivalentTo(contract);
    }

    private static DraftSpec Spec(string id, bool parent) => new(new SpecDraft(id, "g", $"spec: {id}", []), parent);
}
