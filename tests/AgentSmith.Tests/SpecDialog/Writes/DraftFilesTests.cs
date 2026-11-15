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

    /// <summary>2026-11-15-67f1: a ticket a file wrote is that text; one seeded from an older proposal is rendered.</summary>
    [Fact]
    public void Ticket_WithSource_ServesItAndWithoutRenders()
    {
        var ticket = new GraphTicket("a-one", "A", "Do A.", ["A is done"], [], []);
        const string written = "title: A\nrequirement: >\n  Do A.\ndone: [\"A is done\"]";

        DraftFiles.Find(new ProposalDraft { Tickets = [ticket with { Source = written }] }, "tickets/a-one.yaml")!
            .Text.Should().Be(written + "\n");
        DraftFiles.Find(new ProposalDraft { Tickets = [ticket] }, "tickets/a-one.yaml")!
            .Text.Should().Be(DraftTicketText.Render(ticket));
    }

    /// <summary>2026-11-15-67f1: the review key of a contract is its fields, so a manifest's contract keeps its review.</summary>
    [Fact]
    public void ReviewedText_ContractHash_LeavesTheWrittenTextOut()
    {
        var contract = new SeriesContract([], [], [new SeriesContractArea("p1", ["a.cs"])], []);

        ReviewedText.Hash(contract with { Source = "areas: []" }).Should().Be(ReviewedText.Hash(contract));
    }

    [Fact]
    public void SeriesContract_WithinItsMembers_IsTheSameContract()
    {
        var contract = new SeriesContract([], [], [new SeriesContractArea("p1", ["a.cs"])], []) { Source = "x" };

        contract.Within(["p1"]).Contract.Should().BeSameAs(contract);
        contract.Within(["p2"]).Contract.Should().BeNull();
    }

    [Fact]
    public void Bug_RendersAndReadsBack()
    {
        var bug = new BugTicketDraft("Fix: the null", "It throws\nwhen empty.", "- it no longer throws\n- a test covers it");

        var draft = DraftWriteRig.Files().Put(ProposalDraft.Empty, "bug.yaml", DraftBugText.Render(bug),
            new DraftScope([], false)).Written();

        draft.Bug.Should().Be(bug with { Source = DraftBugText.Render(bug) });
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

    [Fact]
    public void SeriesDraftIds_LabelledDrafts_TakeTheirSeriesIdsInTextAndRequires()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, "platform", "goal: \"Platform\"", true, []).Written();
        draft = specs.Write(draft, "store", "goal: \"Store\"", false, []).Written();
        draft = specs.Write(draft, "store-api", "goal: \"Api\"\nrequires: [store]", false, []).Written();

        var filed = new Application.Services.SpecDialog.SeriesDraftIds(new Application.Services.SpecDialog.SpecDraftIdRewriter())
            .Assign("2026-11-15-abcd", [.. draft.Specs.Where(s => !s.Parent).Select(s => s.Draft)]);

        filed.Select(f => f.SpecId).Should().Equal("2026-11-15-abcda", "2026-11-15-abcdb");
        filed[1].Requires.Should().Equal("2026-11-15-abcda");
        filed[1].Yaml.Should().StartWith("spec: 2026-11-15-abcdb").And.Contain("requires: [2026-11-15-abcda]");
    }

    private static DraftSpec Spec(string id, bool parent) => new(new SpecDraft(id, "g", $"spec: {id}", []), parent);
}
