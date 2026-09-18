using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79b: a run never re-cuts a set a person approved. A comment, an edited ticket and
/// a bare re-trigger each still name themselves in the revision the run publishes — and each says
/// the approved set was kept — but none of them puts the model back in front of the cut.
/// </summary>
public sealed class ApprovedSetNotRecutTests
{
    private const string Key = "azdo-19106";
    private const string Text = "Migrate the client.";

    [Fact]
    public async Task ApprovedSet_CommentOnTheTicket_IsNotRecut()
    {
        var harness = Harness(approved: true);
        harness.Comments = [OurCut(), Operator("phase b is wrong: the callers stay where they are")];

        var result = await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        result.IsSuccess.Should().BeTrue();
        harness.Deriver.Calls.Should().Be(0,
            "a person ratified this set; a comment must not re-cut it behind their back");
        harness.Writer.Written!.Phases.Select(p => p.PhaseId).Should().Equal("p19106a", "p19106b");
    }

    [Fact]
    public async Task ApprovedSet_TicketDescriptionEdited_IsNotRecut()
    {
        var harness = Harness(approved: true, fingerprint: "a-fingerprint-of-the-text-before-the-edit");

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Deriver.Calls.Should().Be(0);
        harness.Writer.Written!.Phases.Select(p => p.PhaseId).Should().Equal("p19106a", "p19106b");
    }

    /// <summary>
    /// The most likely re-trigger there is: a run that died before its first phase. That state
    /// re-cuts a set nobody approved, and must not re-cut one somebody did.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_RetriggerWithNoExecutedPhase_IsNotRecut()
    {
        var harness = Harness(approved: true);

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Writer.Written!.Current.Cause.Should().Be(SpecRevisionCause.Retrigger,
            "the pointer names the branch's own commit, so nothing else arrived");
        harness.Deriver.Calls.Should().Be(0);
    }

    /// <summary>
    /// On run one there is no cut comment to measure from, so the WHOLE thread counts as an
    /// answer. The rule this phase adds is asked of the set, not of the thread.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_FirstRunWithNoCutCommentAndAThread_IsStillNotRecut()
    {
        var harness = Harness(approved: true);
        harness.Comments = [Operator("are you sure about the second repository?")];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Deriver.Calls.Should().Be(0);
        harness.Writer.Written!.Current.Cause.Should().StartWith(SpecRevisionCause.Comment);
    }

    [Fact]
    public async Task UnapprovedSet_RetriggerWithNoExecutedPhase_StillReCuts()
    {
        var harness = Harness(approved: false);
        harness.Deriver.Result = Derivation();

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Deriver.Calls.Should().Be(1,
            "a ticket nobody approved amends exactly as it does today");
        harness.Writer.Written!.Current.Cause.Should().Be(SpecRevisionCause.Retrigger);
    }

    [Fact]
    public async Task ApprovedSet_Comment_RevisionNamesTheCauseAndSaysItWasKept()
    {
        var harness = Harness(approved: true);
        harness.Comments = [OurCut(), Operator("please also touch the second repository")];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Writer.Written!.Current.Cause.Should()
            .Be($"{SpecRevisionCause.Comment} — {ApprovedSetKept.Kept}");
    }

    [Fact]
    public async Task ApprovedSet_Comment_PostsOneTicketNoticeAndOneRunDecision()
    {
        var harness = Harness(approved: true);
        harness.Tracker = harness.Notices.Tracker;
        harness.Comments = [OurCut(), Operator("phase b is wrong")];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Notices.Comments.Should().ContainSingle()
            .Which.Should().Contain("comment").And.Contain(ApprovedSetKept.Kept);
        harness.Decisions.Decisions.Should().ContainSingle()
            .Which.Should().Be(harness.Notices.Comments[0], "the run view carries the same sentence");
    }

    [Fact]
    public async Task ApprovedSet_TwoCommentsInOneRun_PostsOneNotice()
    {
        var harness = Harness(approved: true);
        harness.Tracker = harness.Notices.Tracker;
        harness.Comments = [OurCut(), Operator("one thing"), Operator("and another")];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Notices.Comments.Should().ContainSingle("a run reports once, whatever arrived");
    }

    [Fact]
    public async Task ApprovedSet_Comment_RunContinuesAndIsNotParked()
    {
        var harness = Harness(approved: true);
        harness.Comments = [OurCut(), Operator("this is all wrong")];

        var result = await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        result.IsSuccess.Should().BeTrue(
            "a comment is not evidence the spec is wrong; an operator who wants the run stopped has cancel");
        harness.Writer.Written!.IsHandedBack.Should().BeFalse();
    }

    /// <summary>
    /// A ticket EDIT does not clear itself: the fingerprint is refreshed only when the model ran,
    /// and an approved set never runs it. A run that saw the edit and kept the set therefore
    /// publishes the text it saw, so the next run does not report the same edit again.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_TicketEditedOnce_PostsOneNoticeAndNotAgainOnTheNextRun()
    {
        var first = Harness(approved: true, fingerprint: "the-text-before-the-edit");
        first.Tracker = first.Notices.Tracker;

        await first.Handler().ExecuteAsync(first.Context(Ticket()), default);

        first.Notices.Comments.Should().ContainSingle();
        var refreshed = first.Writer.Written!.TicketFingerprint;
        refreshed.Should().Be(TicketTextFingerprint.Of(Ticket()), "the input is recorded and no longer new");

        var next = Harness(approved: true, fingerprint: refreshed);
        next.Tracker = next.Notices.Tracker;

        await next.Handler().ExecuteAsync(next.Context(Ticket()), default);

        next.Notices.Comments.Should().BeEmpty("the same edit is reported once, not on every later run");
        next.Decisions.Decisions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApprovedSet_TicketEditedAgain_ReportsTheNewEdit()
    {
        var harness = Harness(approved: true, fingerprint: TicketTextFingerprint.Of(Ticket()));
        harness.Tracker = harness.Notices.Tracker;

        await harness.Handler().ExecuteAsync(
            harness.Context(Ticket("Migrate the client, and the second repository too.")), default);

        harness.Notices.Comments.Should().ContainSingle()
            .Which.Should().Contain("edited");
        harness.Deriver.Calls.Should().Be(0);
    }

    /// <summary>
    /// The COMMENT cause clears itself: the cut comment is posted on every run and moves the
    /// anchor the comment rule measures from, so the same comment is not an answer twice. Proven
    /// end to end — the run really posts the cut comment, and the next run reads the thread back.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_Comment_TheCutCommentItPostsClearsTheCauseForTheNextRun()
    {
        var harness = Harness(approved: true);
        harness.Tracker = harness.Notices.Tracker;
        var operatorComment = Operator("phase b is wrong");
        harness.Comments = [OurCut(), operatorComment];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Cuts.Comments.Should().ContainSingle("the cut comment is posted on every run")
            .Which.Should().Contain(SpecSetComment.CutMarker);
        var thread = new List<TicketComment>
        {
            operatorComment,
            new("agent-smith", DateTimeOffset.UtcNow, harness.Notices.Comments[0]),
            new("agent-smith", DateTimeOffset.UtcNow.AddSeconds(1), harness.Cuts.Comments[0]),
        };
        OwnTicketComment.IsAnswered(thread, SpecSetComment.CutMarker).Should().BeFalse(
            "the new cut comment is the anchor now, and nobody has commented after it");
    }

    /// <summary>
    /// The refresh is what CLEARS a kept edit, so it must not happen when nobody was told. A
    /// tracker that refuses the comment leaves the fingerprint alone and the next run says it
    /// again — a repeated notice is noise, a cleared cause nobody received loses the edit.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_TicketEditedButTheNoticeWasRefused_IsNotMarkedAsReported()
    {
        var harness = Harness(approved: true, fingerprint: "the-text-before-the-edit");
        harness.Tracker = harness.Notices.Tracker;
        harness.Notices.Refuse = true;

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Notices.Comments.Should().BeEmpty("the tracker refused it");
        harness.Writer.Written!.TicketFingerprint.Should().Be("the-text-before-the-edit",
            "an edit nobody was told about must not be marked as dealt with");
    }

    /// <summary>A run with no tracker has nowhere to report, so it keeps the cause too.</summary>
    [Fact]
    public async Task ApprovedSet_TicketEditedWithNoTracker_KeepsTheCauseForALaterRun()
    {
        var harness = Harness(approved: true, fingerprint: "the-text-before-the-edit");

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Decisions.Decisions.Should().ContainSingle("the run view still gets it");
        harness.Writer.Written!.TicketFingerprint.Should().Be("the-text-before-the-edit");
    }

    /// <summary>
    /// A hand-back used to skip the whole announce block, which would have swallowed the notice
    /// for exactly the run that most needs to say what it saw.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_HandedBackSet_StillReportsTheInput()
    {
        var harness = Harness(approved: true, handback: true);
        harness.Tracker = harness.Notices.Tracker;
        harness.Comments = [OurCut(), Operator("this is wrong")];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Writer.Written!.IsHandedBack.Should().BeTrue();
        harness.Notices.Comments.Should().ContainSingle(
            "the notice is posted before the publish, so a hand-back cannot suppress it");
    }

    /// <summary>
    /// A re-approval that edits a phase which already ran has that edit discarded by the merge.
    /// The person who made it is TOLD, on the ticket — the log and set.yaml are not where they
    /// look, and this phase built the notice that reaches them.
    /// </summary>
    [Fact]
    public async Task Reapproval_EditingAnExecutedPhase_TellsTheTicketItWasDiscarded()
    {
        var harness = Harness(approved: true, executed: "p19106a");
        harness.Tracker = harness.Notices.Tracker;
        var edited = new SpecApprovalRecord(
            Key,
            ApprovedSets.Set(
                Key,
                [ApprovedSets.Phase("p19106a", "Rewritten after it ran"), ApprovedSets.Phase("p19106b")],
                ApprovedSets.Approval(ApprovedSets.Noon.AddHours(2), "session-99")),
            [],
            ApprovedSets.Tracker);
        await harness.Approvals.SaveAsync(edited, default);

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Writer.Written!.Phases[0].Draft.Goal.Should().Be("Goal p19106a",
            "the executed phase is kept exactly as it ran");
        harness.Notices.Comments.Should().ContainSingle()
            .Which.Should().Contain("discarded").And.Contain("p19106a");
    }

    /// <summary>
    /// The question pin is about a question THIS framework asked, not about the model being asked
    /// to cut again — it is deliberately left alone.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_UnansweredQuestionPin_StillPins()
    {
        var harness = Harness(approved: true, handback: true);
        harness.Tracker = harness.Notices.Tracker;
        harness.Comments = [];
        var context = harness.Context(Ticket());

        await harness.Handler().ExecuteAsync(context, default);

        context.Pipeline.Has(ContextKeys.SpecQuestionPin).Should().BeTrue(
            "an unanswered question is still pinned as the answer");
        harness.Deriver.Calls.Should().Be(0);
    }

    private static ApprovedSetHarness Harness(
        bool approved, string? fingerprint = null, bool handback = false, string? executed = null)
    {
        var harness = new ApprovedSetHarness { Branch = { Key = Key }, PointerSha = "branch-sha" };
        harness.Branch.SeedSet(
            BranchYaml(fingerprint ?? TicketTextFingerprint.Of(Ticket()), approved, handback, executed),
            new Dictionary<string, string>
            {
                ["p19106a-first"] = PhaseYaml("p19106a"),
                ["p19106b-second"] = PhaseYaml("p19106b"),
            });
        return harness;
    }

    private static string BranchYaml(
        string fingerprint, bool approved, bool handback, string? executed = null)
    {
        var lines = new List<string>
        {
            $"key: {Key}",
            "source: Approved",
            "phases:",
            "- p19106a-first",
            "- p19106b-second",
            "revisions:",
            "- number: 1",
            "  cause: approved in design conversation session-77",
            "  at: 2026-09-17T12:00:00.0000000+00:00",
            $"ticket_fingerprint: {fingerprint}",
        };
        if (executed is not null) lines.AddRange(["executed_phases:", $"- {executed}"]);
        if (approved)
            lines.AddRange([
                "approved_at: 2026-09-17T12:00:00.0000000+00:00",
                "approved_in_conversation: session-77",
                "approved_by: sample.approver",
            ]);
        if (handback)
            lines.AddRange([
                "handback_case: Question",
                "handback_reason: which of the two clients is meant?",
                "handback_readings:",
                "- the desktop client",
                "- the mobile client",
                "handback_taken: 1",
            ]);
        return string.Join("\n", lines);
    }

    private static string PhaseYaml(string id) => $"""
        phase: {id}
        goal: "Goal {id}"
        done:
          - "Done {id}."
        """;

    private static Ticket Ticket(string description = Text) =>
        new(new TicketId("19106"), "Migrate the client", description, null, "open", "azdo", []);

    private static TicketComment OurCut() => new(
        "agent-smith", DateTimeOffset.UtcNow.AddHours(-2),
        SpecSetComment.Render(
            new SpecSet(
                Key, [ApprovedSets.Phase("p19106a")], SpecAccounting.Empty,
                [new SpecRevision(1, SpecRevisionCause.Initial, ApprovedSets.Noon)],
                SpecSource.BranchArtifact),
            null));

    private static TicketComment Operator(string body) =>
        new("operator", DateTimeOffset.UtcNow.AddHours(-1), body);

    private static SpecDerivation Derivation()
    {
        var segments = TicketSegmenter.Segment(Text);
        var phase = new SpecPhase(
            ApprovedSets.Phase("p19106a").Draft, "p19106a", string.Empty, [.. segments.Select(s => s.Id)]);
        return new SpecDerivation(
            new SpecSet(
                Key, [phase], SpecAccountingBuilder.Build([phase], [], segments),
                [new SpecRevision(1, SpecRevisionCause.Initial, ApprovedSets.Noon)],
                SpecSource.Derived),
            []);
    }
}
