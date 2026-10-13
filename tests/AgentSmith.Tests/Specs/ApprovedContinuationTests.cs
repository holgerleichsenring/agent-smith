using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-13-9a59l: the run applies an approval given in a worked ticket's conversation to the series
/// the branch holds NOW — by spec id, keeping what ran as it ran, and naming what changed since.
/// </summary>
public sealed class ApprovedContinuationTests
{
    private const string Series = "2026-10-06-0a0a";
    private const string Key = "azdo-19106";
    private static readonly string A = Series + "a", B = Series + "b", C = Series + "c";
    private static readonly DateTimeOffset Later = ApprovedSets.Noon.AddHours(3);

    private readonly ApprovedContinuation _sut = ApprovedSetDoubles.Continuation();

    [Fact]
    public void ApprovedContinuation_AppliesDispositions_KeepsExecutedHeadAndLabels()
    {
        var branch = Branch([A], Spec(A, "a-label"), Spec(B, "b-label"), Spec(C, "c-label"));
        var record = Record([Spec(B, "new-label", "B, re-cut"), Spec("x", "x-label", "New work")],
            (A, SpecDispositionKinds.Kept), (B, SpecDispositionKinds.Recut), (C, SpecDispositionKinds.Dropped),
            ("x", SpecDispositionKinds.Appended));

        var decision = _sut.Decide(branch, record, SpecRevisionCause.Retrigger)!;

        decision.Set!.Specs.Select(s => (s.SpecId, s.Slug, s.Draft.Goal)).Should().Equal(
            (A, "a-label", $"Goal {A}"), (B, "b-label", "B, re-cut"), ("x", "x-label", "New work"));
        decision.Cause.Should().StartWith(SpecRevisionCause.Approval);
        decision.Approval.Should().Be(record.Approval);
        decision.NeedsModel.Should().BeFalse();
        decision.Discarded.Should().BeNull();
    }

    [Fact]
    public void ApprovedContinuation_SpecRanSinceBasis_KeptAndReportedDiscarded()
    {
        var branch = Branch([A, B], Spec(A, "a"), Spec(B, "b"));
        var record = Record([Spec(B, "b", "B, re-cut")], (A, SpecDispositionKinds.Kept), (B, SpecDispositionKinds.Recut));

        var decision = _sut.Decide(branch, record, SpecRevisionCause.Retrigger)!;

        decision.Set!.Specs.Single(s => s.SpecId == B).Draft.Goal.Should().Be($"Goal {B}", "it ran as it ran");
        decision.Discarded.Should().Contain(B).And.Contain("discarded");
    }

    [Fact]
    public void ApprovedContinuation_AppendedIdTakenOnBranch_TakesNextFree()
    {
        var branch = Branch([A], Spec(A, "a"), Spec(B, "b"), Spec(C, "c-pushed-by-hand"));
        var record = Record([Spec(C, "c", "Approved new work")],
            (A, SpecDispositionKinds.Kept), (B, SpecDispositionKinds.Kept), (C, SpecDispositionKinds.Appended));

        var decision = _sut.Decide(branch, record, SpecRevisionCause.Retrigger)!;

        decision.Set!.Specs.Select(s => (s.SpecId, s.Draft.Goal)).Should().Equal(
            (A, $"Goal {A}"), (B, $"Goal {B}"), (Series + "d", "Approved new work"), (C, $"Goal {C}"));
        decision.Discarded.Should().Contain(C, "a spec the basis never saw is named");
    }

    [Fact]
    public void ApprovedContinuation_SameApproval_IsNoOp()
    {
        var record = Record([Spec(B, "b", "B, re-cut")], (B, SpecDispositionKinds.Recut));
        var branch = Branch([A], Spec(A, "a"), Spec(B, "b")) with { Approval = record.Approval };

        _sut.Decide(branch, record, SpecRevisionCause.Retrigger).Should().BeNull("this approval is already on the branch");
    }

    [Fact]
    public void ApprovedContinuation_WithoutBasisRunId_NeverMerged()
    {
        var record = Record([Spec(B, "b", "B, re-cut")], (B, SpecDispositionKinds.Recut)) is var r
            ? r with { Continuation = r.Continuation! with { BasisRunId = null } } : null;

        _sut.Decide(Branch([], Spec(A, "a"), Spec(B, "b")), record, SpecRevisionCause.Retrigger).Should().BeNull();
    }

    [Fact]
    public void ApprovedContinuation_OverCap_NotMergedNoticeSaysWhy()
    {
        var ran = Enumerable.Range(0, 8).Select(i => SeriesIdFactory.Member(Series, i)).ToList();
        var branch = Branch(ran, [.. ran.Select(id => Spec(id, id))]);
        var record = Record([Spec("x", "x", "One too many")], ("x", SpecDispositionKinds.Appended));

        var decision = _sut.Decide(branch, record, SpecRevisionCause.Retrigger)!;

        decision.Set!.Specs.Should().HaveCount(8, "nothing is merged");
        decision.Approval.Should().BeNull();
        decision.Discarded.Should().Contain("not merged").And.Contain("8 executed").And.Contain("1 unstarted");
    }

    [Fact]
    public async Task ApprovedContinuation_OverHandedBackSet_ClearsHandback_SpecGateRuns()
    {
        var harness = Harness(handback: true);
        await harness.Approvals.SaveAsync(HarnessRecord(), default);
        var context = harness.Context(Ticket());

        (await harness.Handler().ExecuteAsync(context, default)).IsSuccess.Should().BeTrue();

        harness.Writer.Written!.IsHandedBack.Should().BeFalse("the approval answers the hand-back");
        var gate = await new SpecGateHandler(NullLogger<SpecGateHandler>.Instance)
            .ExecuteAsync(new SpecGateContext(Ticket(), context.Pipeline), default);
        gate.Message.Should().NotContain("nothing to gate").And.Contain("validated");
    }

    [Fact]
    public void ApprovedSetHandoff_ContinuationRecord_NothingAtPath_RefusedNotHandedOver()
    {
        var resolver = new SpecSourceResolver(new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance),
            ApprovedSetDoubles.Continuation(), new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
            NullLogger<SpecSourceResolver>.Instance);

        var decision = resolver.Decide(SpecSetOnBranch.Nothing, Ticket(), null, new PipelineContext(), Key,
            TicketLabelVocabulary.Default, Record([Spec("x", "x", "New")], ("x", SpecDispositionKinds.Appended)));

        decision.Set.Should().BeNull("a continuation's set is the proposal, never the whole series");
        decision.Failure.Should().Contain("continues the series").And.Contain("no longer holds");
    }

    /// <summary>The approval recorded by the conversation, then the ticket's next run over a branch whose
    /// head already ran: the head stays as it ran, the re-cut lands, the new spec is appended.</summary>
    [Fact]
    public async Task Harness_ApprovalThenNextRun_BranchSeriesKeepsHeadAndAppends()
    {
        var harness = Harness(handback: false, executed: "p19106a");
        harness.Tracker = harness.Notices.Tracker;
        await harness.Approvals.SaveAsync(HarnessRecord(), default);

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Writer.Written!.Specs.Select(s => (s.SpecId, s.Draft.Goal)).Should().Equal(
            ("p19106a", "Goal p19106a"), ("p19106b", "B, re-cut"), ("p19106c", "Appended work"));
        harness.Writer.Written.Current.Cause.Should().StartWith(SpecRevisionCause.Approval);
        harness.Writer.Written.Approval!.Conversation.Should().Be("session-continue");
        harness.Deriver.Calls.Should().Be(0);
        harness.Notices.Comments.Should().ContainSingle("the discarded edit to the spec that ran is named")
            .Which.Should().Contain("p19106a");
    }

    private static SpecApprovalRecord HarnessRecord() => new(Key,
        ApprovedSets.Set(Key, [Spec("p19106a", "first", "A rewritten"), Spec("p19106b", "second", "B, re-cut"),
            Spec("p19106c", "third", "Appended work")], ApprovedSets.Approval(Later, "session-continue")) with { Series = Series },
        ["primary"], ApprovedSets.Tracker,
        Continuation: new SeriesContinuation([new("p19106a", SpecDispositionKinds.Recut),
            new("p19106b", SpecDispositionKinds.Recut), new("p19106c", SpecDispositionKinds.Appended)], "r-1"));

    private static SpecSet Branch(IReadOnlyList<string> executed, params DerivedSpec[] specs) =>
        new(Key, specs, SpecAccounting.Empty, [], SpecSource.BranchArtifact, ExecutedSpecIds: executed,
            Approval: ApprovedSets.Approval(ApprovedSets.Noon), Series: Series);

    private static SpecApprovalRecord Record(IReadOnlyList<DerivedSpec> approved, params (string Id, string Kind)[] kinds) =>
        new(Key, ApprovedSets.Set(Key, approved, ApprovedSets.Approval(Later, "session-continue")) with { Series = Series },
            [], ApprovedSets.Tracker,
            Continuation: new SeriesContinuation([.. kinds.Select(k => new SpecDisposition(k.Id, k.Kind))], "r-1"));

    private static DerivedSpec Spec(string id, string slug, string? goal = null) =>
        new(new SpecDraft(id, goal ?? $"Goal {id}", $"spec: {id}\ngoal: \"{goal ?? $"Goal {id}"}\"", []) { Done = [$"Done {id}."] },
            slug, string.Empty, []);

    private static Ticket Ticket() => new(new TicketId("19106"), "A ticket", "Migrate the client.", null, "open", "azdo", []);

    private static ApprovedSetHarness Harness(bool handback, string? executed = null)
    {
        var harness = new ApprovedSetHarness { Branch = { Key = Key }, PointerSha = "branch-sha" };
        var lines = new List<string>
        {
            $"ticket: {Key}", "specs:", "- p19106a", "- p19106b", "revisions:", "- number: 1",
            "  cause: approved in design conversation session-77", "  at: 2026-09-17T12:00:00.0000000+00:00",
            $"ticket_fingerprint: {TicketTextFingerprint.Of(Ticket())}",
            "approved_at: 2026-09-17T12:00:00.0000000+00:00", "approved_in_conversation: session-77",
            "approved_by: sample.approver",
        };
        if (handback)
            lines.AddRange(["handback_case: Question", "handback_reason: which client?", "handback_readings:",
                "- the desktop client", "- the mobile client", "handback_taken: 1"]);
        harness.Branch.SeedSeries(Series, string.Join("\n", lines), new Dictionary<string, string>
        {
            ["p19106a-first"] = $"spec: p19106a\ngoal: \"Goal p19106a\"\ndone:\n  - \"Done p19106a.\"",
            ["p19106b-second"] = $"spec: p19106b\ngoal: \"Goal p19106b\"\ndone:\n  - \"Done p19106b.\"",
        }, executed);
        return harness;
    }
}
