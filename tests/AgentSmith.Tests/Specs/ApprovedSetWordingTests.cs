using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79b: what the ticket is told. The cut comment stops promising an amendment for a
/// set a person approved and points at the branch specs, which is the route that WORKS today; the
/// kept-set notice is OUR comment and deliberately is not a cut comment, because the cut marker is
/// the anchor the comment rule measures from.
/// </summary>
public sealed class ApprovedSetWordingTests
{
    private const string Key = "azdo-19106";

    /// <summary>
    /// It must not send the operator to the design conversation: nothing re-opens an approved
    /// record from one yet, and that would be the deleted promise moved somewhere else.
    /// </summary>
    [Fact]
    public void CutComment_ApprovedSet_PointsAtTheBranchSpecsInsteadOfAnAmendment()
    {
        var body = SpecSetComment.Render(Set(approved: true), null);

        body.Should().Contain("edit the phase specs on the ticket branch")
            .And.Contain("will NOT change the specification");
        body.Should().NotContain("re-cuts the unstarted tail",
            "for an approved set that promise is false and contradicts the notice beside it");
        body.Should().NotContain("approving the set again",
            "no conversation can re-open an approved record yet — pointing there would be unreachable");
    }

    [Fact]
    public void CutComment_UnapprovedSet_KeepsTodaysWording()
    {
        var body = SpecSetComment.Render(Set(approved: false), null);

        body.Should().Contain("comment if the cut is wrong")
            .And.Contain("re-cuts the unstarted tail");
        body.Should().NotContain("will NOT change the specification");
    }

    [Fact]
    public void CutComment_BothWordings_CarryTheCutMarker_AndAreNotTheSameSentence()
    {
        var approved = SpecSetComment.Render(Set(approved: true), null);
        var derived = SpecSetComment.Render(Set(approved: false), null);

        approved.Should().Contain(SpecSetComment.CutMarker);
        derived.Should().Contain(SpecSetComment.CutMarker);
        approved.Should().NotBe(derived,
            "an approved set is told something different from a set the run cut itself");
    }

    [Fact]
    public void ApprovedSetNotice_IsOurComment_AndIsNotACutComment()
    {
        var body = ApprovedSetKept.Notice(Set(approved: true), SpecRevisionCause.Comment);

        body.Should().NotBeNull();
        OwnTicketComment.IsOurs(Comment(body!)).Should().BeTrue(
            "a notice without our marker would count as somebody answering, next run");
        body.Should().NotContain(SpecSetComment.CutMarker,
            "a second comment carrying the cut marker would move the anchor past the operator's own");
    }

    [Fact]
    public void ApprovedSetNotice_NextRun_DoesNotReadItAsAForeignComment()
    {
        var cut = new TicketComment(
            "agent-smith", ApprovedSets.Noon, SpecSetComment.Render(Set(approved: true), null));
        var notice = new TicketComment(
            "agent-smith", ApprovedSets.Noon.AddMinutes(1),
            ApprovedSetKept.Notice(Set(approved: true), SpecRevisionCause.Comment)!);

        OwnTicketComment.IsAnswered([cut, notice], SpecSetComment.CutMarker).Should().BeFalse(
            "our own notice is not the operator commenting again");
    }

    /// <summary>The notice names where a change is actually made, not a conversation id nobody
    /// can navigate to — the conversation is named as provenance only.</summary>
    [Fact]
    public void ApprovedSetNotice_SaysWhereAChangeIsMade()
    {
        var body = ApprovedSetKept.Notice(Set(approved: true), SpecRevisionCause.Comment)!;

        body.Should().Contain(".agentsmith/specs/").And.Contain("pull request");
        body.Should().Contain("session-77", "the conversation is the provenance of the approval");
    }

    [Fact]
    public void ApprovedSetNotice_NothingArrived_IsNotPosted()
    {
        ApprovedSetKept.Notice(Set(approved: true), SpecRevisionCause.Retrigger).Should().BeNull();
        ApprovedSetKept.Notice(Set(approved: false), SpecRevisionCause.Comment).Should().BeNull(
            "a set nobody approved was re-cut, so there is nothing to say was kept");
    }

    /// <summary>
    /// A cause names ONE input and an edit outranks a comment, so a run that saw both would
    /// otherwise tell the operator about the edit and drop their comment silently.
    /// </summary>
    [Fact]
    public void ApprovedSetNotice_AnEditAndAComment_NamesBoth()
    {
        var body = ApprovedSetKept.Notice(
            Set(approved: true), SpecRevisionCause.TicketEdit, alsoCommented: true)!;

        body.Should().Contain("edited").And.Contain("comment arrived");
    }

    /// <summary>An edit the merge threw away is reported, not left in the log and set.yaml where
    /// the person who made it never looks.</summary>
    [Fact]
    public void ApprovedSetNotice_ADiscardedHeadEdit_IsReportedEvenWithNoOtherInput()
    {
        var body = ApprovedSetKept.Notice(
            Set(approved: true), $"{SpecRevisionCause.Approval} session-77",
            discarded: "the re-approved set edits p19106a, which already ran");

        body.Should().NotBeNull();
        body.Should().Contain("discarded").And.Contain("p19106a");
    }

    [Fact]
    public void ApprovedSetKept_CauseFor_AddsTheKeptPhraseOnlyForAnApprovedInput()
    {
        ApprovedSetKept.CauseFor(Set(approved: true), SpecRevisionCause.TicketEdit)
            .Should().Be($"{SpecRevisionCause.TicketEdit} — {ApprovedSetKept.Kept}");
        ApprovedSetKept.CauseFor(Set(approved: false), SpecRevisionCause.TicketEdit)
            .Should().Be(SpecRevisionCause.TicketEdit, "the constants keep their exact values");
        ApprovedSetKept.CauseFor(Set(approved: true), SpecRevisionCause.Resume)
            .Should().Be(SpecRevisionCause.Resume);
    }

    [Fact]
    public void ApprovedSetKept_SawAnEdit_IsTrueOnlyForAKeptTicketEdit()
    {
        ApprovedSetKept.SawAnEdit(Set(approved: true), SpecRevisionCause.TicketEdit).Should().BeTrue();
        ApprovedSetKept.SawAnEdit(Set(approved: true), SpecRevisionCause.Comment).Should().BeFalse();
        ApprovedSetKept.SawAnEdit(Set(approved: false), SpecRevisionCause.TicketEdit).Should().BeFalse();
    }

    private static TicketComment Comment(string body) =>
        new("agent-smith", ApprovedSets.Noon, body);

    private static SpecSet Set(bool approved) => new(
        Key,
        [ApprovedSets.Phase("p19106a")],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, ApprovedSets.Noon)],
        approved ? SpecSource.Approved : SpecSource.Derived,
        Approval: approved ? ApprovedSets.Approval(ApprovedSets.Noon, "session-77") : null);
}
