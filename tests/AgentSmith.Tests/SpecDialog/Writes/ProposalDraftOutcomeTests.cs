using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

public sealed class ProposalDraftOutcomeTests
{
    [Fact]
    public void ProposalDraftOutcome_ParentAndChildren_IsEpic()
    {
        var draft = new ProposalDraft { Specs = [Spec("p9000", true), Spec("p9000a", false)] };

        var epic = ProposalDraftOutcome.Of(draft).Should().BeOfType<EpicOutcome>().Subject;

        epic.Parent.SpecId.Should().Be("p9000");
        epic.Children.Select(c => c.SpecId).Should().Equal("p9000a");
    }

    [Fact]
    public void ProposalDraftOutcome_Unchanged_IsAnswer()
    {
        var store = new ProposalStore();
        store.Seed(new ProposalDraft { Specs = [Spec("p9001", false)] }, changed: false);

        ProposalDraftOutcome.For(store).Should().BeOfType<AnswerOutcome>();
    }

    [Fact]
    public void ProposalDraftOutcome_EachKind_IsWhatTheDraftHolds()
    {
        ProposalDraftOutcome.Of(new ProposalDraft { Specs = [Spec("p9001", false)] }).Should().BeOfType<SpecOutcome>();
        ProposalDraftOutcome.Of(new ProposalDraft { Tickets = [DraftWriteRig.Ticket("a-one")] }).Should().BeOfType<TicketGraphOutcome>();
        ProposalDraftOutcome.Of(new ProposalDraft { Bug = new BugTicketDraft("t", "d", null) }).Should().BeOfType<BugOutcome>();
        ProposalDraftOutcome.Of(new ProposalDraft { Comment = "c" }).Should().BeOfType<TicketCommentOutcome>();
        ProposalDraftOutcome.Of(ProposalDraft.Empty).Should().BeOfType<AnswerOutcome>();
    }

    [Fact]
    public void ProposalDraftSeed_Epic_RoundTripsThroughTheDraft()
    {
        var epic = new EpicOutcome(Spec("p9000", true).Draft, [Spec("p9000a", false).Draft]);

        ProposalDraftOutcome.Of(ProposalDraftSeed.Of(epic)).Should().BeEquivalentTo(epic);
    }

    [Fact]
    public void ProposalStore_RefusedWrite_LeavesTheDraftAndChangedAsTheyWere()
    {
        var store = new ProposalStore();

        store.Apply(_ => DraftWrite.Refused("no")).Should().Be("no");

        store.Changed.Should().BeFalse();
        store.Current.Should().BeSameAs(ProposalDraft.Empty);
    }

    [Fact]
    public void ContractDraftWrite_WithoutParent_IsRefused()
    {
        using var contract = JsonDocument.Parse("""{"names": []}""");

        DraftWriteRig.Contracts().Write(ProposalDraft.Empty, contract.RootElement).Refusal
            .Should().Contain("belongs to an epic");
    }

    [Fact]
    public void SeriesContractReader_FromDraft_NotAnObject_IsAProblem()
    {
        using var contract = JsonDocument.Parse("[]");
        var draft = new ProposalDraft { Specs = [Spec("p9000", true), Spec("p9000a", false)] };

        DraftWriteRig.Contracts().Write(draft, contract.RootElement).Refusal.Should().Contain("it is not an object");
    }

    [Fact]
    public void BugDraftWrite_Criteria_BecomeListedLines()
    {
        var bug = BugDraftWrite.Write(ProposalDraft.Empty, "Fix it", "It breaks.", ["the case passes", " "]).Written().Bug;

        bug.Should().Be(new BugTicketDraft("Fix it", "It breaks.", "- the case passes"));
    }

    [Fact]
    public void CommentDraftWrite_Empty_IsRefused()
    {
        CommentDraftWrite.Write(ProposalDraft.Empty, "  ").Refusal.Should().Be("the comment is empty");
    }

    [Fact]
    public void ProposalDraftSection_SeededDraft_ShowsEachItemByItsHandle()
    {
        var pipeline = new PipelineContext();
        var slot = new SpecDialogReplySlot();
        slot.Proposal.Seed(new ProposalDraft { Tickets = [DraftWriteRig.Ticket("a-one")] }, changed: false);
        pipeline.Set(ContextKeys.SpecDialogReplySlot, slot);

        ProposalDraftSection.Render(pipeline).Should().Contain("## Your draft").And.Contain("### ticket a-one: Title of a-one");
    }

    private static DraftSpec Spec(string id, bool parent) =>
        new(new SpecDraft(id, $"goal {id}", $"spec: {id}\ngoal: \"goal {id}\"", []), parent);
}
