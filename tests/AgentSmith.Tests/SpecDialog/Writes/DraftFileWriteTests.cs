using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

/// <summary>2026-11-15-06fc: one draft file read into the check of its kind.</summary>
public sealed class DraftFileWriteTests
{
    private static readonly DraftScope Unbound = new(DraftWriteRig.Scope, Bound: false);

    [Fact]
    public void Put_Ticket_ReadsTheFileIntoTheTicketCheck()
    {
        const string written = "title: A\nrequirement: Do A.\ndone:\n  - A is done\nrepos: [api]\n";
        var draft = DraftWriteRig.Files().Put(ProposalDraft.Empty, "draft:tickets/a-one.yaml", written, Unbound).Written();

        draft.Tickets.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new GraphTicket("a-one", "A", "Do A.", ["A is done"], [], ["api"]) { Source = written });
    }

    [Fact]
    public void Put_TicketRequiringNoTicketOfTheDraft_IsRefused()
    {
        DraftWriteRig.Files().Put(ProposalDraft.Empty, "tickets/b-two.yaml",
            "title: B\nrequirement: Do B.\ndone: [B is done]\nrequires: [a-one]\n", Unbound).Refusal.Should().Contain("a-one");
    }

    [Fact]
    public void Put_Bug_ReadsTitleDescriptionAndCriteria()
    {
        const string written = "title: Fix the null\ndescription: It throws.\nacceptance_criteria:\n  - it no longer throws\n";
        var draft = DraftWriteRig.Files().Put(ProposalDraft.Empty, "bug.yaml", written, Unbound).Written();

        draft.Bug.Should().Be(new BugTicketDraft("Fix the null", "It throws.", "- it no longer throws") { Source = written });
    }

    [Fact]
    public void Put_Comment_OnlyOnABoundConversation()
    {
        var files = DraftWriteRig.Files();

        files.Put(ProposalDraft.Empty, "comment.md", "Please split it.", Unbound).Refusal
            .Should().Contain("bound to a ticket");
        files.Put(ProposalDraft.Empty, "comment.md", "Please split it.", Unbound with { Bound = true }).Written()
            .Comment.Should().Be("Please split it.");
    }

    [Fact]
    public void Put_ContractWithoutAnEpic_IsRefused()
    {
        DraftWriteRig.Files().Put(ProposalDraft.Empty, "contract.yaml", "areas:\n  - {spec: p1, paths: [a.cs]}", Unbound)
            .Refusal.Should().Contain("write its parent as draft:epic/<id>.yaml first");
    }

    [Fact]
    public void Delete_FileTheDraftDoesNotHold_NamesWhatItHolds()
    {
        var draft = new ProposalDraft { Bug = new BugTicketDraft("t", "d", null) };

        DraftWriteRig.Files().Delete(draft, "draft:specs/p9.yaml").Refusal
            .Should().Contain("draft:specs/p9.yaml is no file of this draft — it holds draft:bug.yaml");
    }

    [Fact]
    public void Delete_EpicDirectoryWithSeveralChildren_IsRefused()
    {
        var draft = new ProposalDraft { Specs = [Spec("p0", true), Spec("p1", false), Spec("p2", false)] };

        DraftWriteRig.Files().Delete(draft, "draft:epic/").Refusal.Should().Contain("the epic's 2 children stay");
        DraftWriteRig.Files().Delete(draft, "draft:specs").Written().Specs.Should().ContainSingle().Which.Parent.Should().BeTrue();
    }

    private static DraftSpec Spec(string id, bool parent) => new(new SpecDraft(id, "g", $"spec: {id}", []), parent);
}
