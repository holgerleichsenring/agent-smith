using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-08-4aa9: the revision cause after an executed phase. The marker's commit is
/// this system's own — the pointer names it, so it reads as a re-trigger, never as a
/// reviewer's edit. A comment by anyone but us after our last cut comment is a comment
/// cause that outranks the sha; a ticket edit and a resume outrank the comment.
/// </summary>
public sealed class SpecRevisionCauseCommentTests
{
    private const string Text = "Users are signed out mid-session.";
    private const string MarkerSha = "marker-sha-2";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Cause_AnExecutedPhaseRecordedByThisSystem_IsARetriggerNotAReviewerEdit()
    {
        SpecRevisionCause.For(Previous(), Pointer(MarkerSha), Ticket(Text), Pipeline())
            .Should().Be(SpecRevisionCause.Retrigger, "the marker moved the pointer to its own commit");
    }

    [Fact]
    public void Cause_AnOperatorCommentAfterOurCutComment_IsAComment()
    {
        var thread = Pipeline(OurCut(Now.AddHours(-2)), Operator(Now.AddHours(-1)));

        SpecRevisionCause.For(Previous(), Pointer(MarkerSha), Ticket(Text), thread)
            .Should().Be(SpecRevisionCause.Comment);
        SpecRevisionCause.For(Previous(), Pointer("another-sha"), Ticket(Text), thread)
            .Should().Be(SpecRevisionCause.Comment, "the comment is newer input than any commit on the branch");
    }

    [Fact]
    public void Cause_OurCutCommentWithNoReply_IsNotAComment()
    {
        var thread = Pipeline(Operator(Now.AddHours(-3)), OurCut(Now.AddHours(-2)));

        SpecRevisionCause.For(Previous(), Pointer(MarkerSha), Ticket(Text), thread)
            .Should().Be(SpecRevisionCause.Retrigger, "the comment before our cut was already in view when we cut");
    }

    [Fact]
    public void Cause_ATicketEdit_OutranksAComment()
    {
        var thread = Pipeline(OurCut(Now.AddHours(-2)), Operator(Now.AddHours(-1)));

        SpecRevisionCause.For(Previous(), Pointer(MarkerSha), Ticket("The text was edited too."), thread)
            .Should().Be(SpecRevisionCause.TicketEdit);
    }

    [Fact]
    public void Cause_AResume_OutranksAComment()
    {
        var resuming = Pipeline(OurCut(Now.AddHours(-2)), Operator(Now.AddHours(-1)));
        resuming.Set(ContextKeys.ResumeCheckpoint, "checkpoint-1");

        SpecRevisionCause.For(Previous(), Pointer(MarkerSha), Ticket(Text), resuming)
            .Should().Be(SpecRevisionCause.Resume, "a resume continues what the run was doing");
    }

    private static PipelineContext Pipeline(params TicketComment[] comments)
    {
        var pipeline = new PipelineContext();
        if (comments.Length > 0)
            pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, comments);
        return pipeline;
    }

    private static TicketComment OurCut(DateTimeOffset at) =>
        new("agent-smith", at, SpecSetComment.Render(Previous().Set, null));

    private static TicketComment Operator(DateTimeOffset at) =>
        new("operator", at, "phase b is wrong: the callers stay where they are");

    private static Ticket Ticket(string description) =>
        new(new TicketId("1"), "Token refresh drops the session", description, null, "open", "azdo", []);

    // A two-phase set whose first phase ran, cut from the unedited text; the marker's
    // commit is the last on the spec path.
    private static SpecSetReadResult Previous() => new(
        new SpecSet(
            "azdo-1", [Phase("p1a"), Phase("p1b")], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, Now.AddHours(-3))],
            SpecSource.BranchArtifact, ExecutedPhaseIds: ["p1a"],
            TicketFingerprint: TicketTextFingerprint.Of(Ticket(Text))),
        MarkerSha);

    private static SpecPhase Phase(string id) => new(
        new PhaseDraft(id, $"Goal {id}", $"phase: {id}\ngoal: \"Goal {id}\"", []) { Done = [$"Done {id}."] },
        id, string.Empty, []);

    private static SpecSetPointer Pointer(string sha) => new("azdo-1", "primary", sha, 1);
}
