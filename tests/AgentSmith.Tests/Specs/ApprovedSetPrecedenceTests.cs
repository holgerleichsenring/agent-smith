using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79a: the branch artifact wins UNLESS a record carries a NEWER approval than the
/// one set.yaml was published from — and when it does, the merge is POSITIONAL over the branch's
/// executed head, because a carried record has no executed phases of its own and reading its tail
/// would re-run every phase that already ran.
/// </summary>
public sealed class ApprovedSetPrecedenceTests
{
    private const string Key = "azdo-19106";

    private readonly ApprovedSetSource _sut = new(NullLogger<ApprovedSetSource>.Instance);

    [Fact]
    public void ApprovedSet_OlderThanTheBranchApproval_LosesToTheBranchArtifact()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a"], executed: []);
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(-1));

        _sut.Decide(record, branch, Key).Should().BeNull(
            "everything else about the branch keeps winning, which is what an executed phase needs");
    }

    [Fact]
    public void Reapproval_NewerThanTheBranchApproval_BeatsTheBranchArtifact()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a"], executed: []);
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(1), ["p0001a", "p0001b"]);

        var decision = _sut.Decide(record, branch, Key);

        decision.Should().NotBeNull();
        decision!.Source.Should().Be(SpecSource.Approved);
        decision.NeedsModel.Should().BeFalse("an approved set is read, never generated");
        decision.Set!.Phases.Select(p => p.PhaseId).Should().Equal("p0001a", "p0001b");
    }

    [Fact]
    public void Reapproval_Published_KeepsTheExecutedHeadAndTheRevisionHistory()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a", "p0001b"], executed: ["p0001a"]);
        var record = ApprovedSets.Record(
            Key, ApprovedSets.Noon.AddHours(1), ["p0001a", "p0001b", "p0001c"]);

        var merged = _sut.Decide(record, branch, Key)!.Set!;

        merged.Phases[0].Should().BeSameAs(branch.Set.Phases[0], "the executed head is the branch's, exactly");
        merged.Revisions.Should().BeEquivalentTo(branch.Set.Revisions,
            "the history the run appends to is the branch's, not the record's");
    }

    [Fact]
    public void Reapproval_Published_TakesTheRecordsPhasesFromTheHeadsLengthOnward()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a", "p0001b"], executed: ["p0001a"]);
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(1), ["p0001a", "p0001x", "p0001y"]);

        var merged = _sut.Decide(record, branch, Key)!.Set!;

        merged.Phases.Select(p => p.PhaseId).Should().Equal(["p0001a", "p0001x", "p0001y"],
            "one executed phase, then the record's phases from index one onward");
    }

    [Fact]
    public void Reapproval_Published_DoesNotEmptyExecutedPhases()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a", "p0001b"], executed: ["p0001a"]);
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(1), ["p0001a", "p0001b"]);

        var merged = _sut.Decide(record, branch, Key)!.Set!;

        merged.Executed.Should().Equal(["p0001a"],
            "an empty executed list would splice every phase into the sequence again");
    }

    [Fact]
    public void Reapproval_ShorterThanTheExecutedHead_IsRefusedNamingWhatItWouldDrop()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a", "p0001b"], executed: ["p0001a", "p0001b"]);
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(1), ["p0001a"]);

        var decision = _sut.Decide(record, branch, Key);

        decision!.Set.Should().BeNull();
        decision.Error.Should().Contain("p0001b")
            .And.Contain("append-only", "the run says which finished phase it refuses to drop");
    }

    [Fact]
    public void Reapproval_EditsAnExecutedPhase_IsDiscardedAndSaidSo()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a", "p0001b"], executed: ["p0001a"]);
        var edited = ApprovedSets.Phase("p0001a", "A different goal than the one that ran");
        var record = new SpecApprovalRecord(
            Key,
            ApprovedSets.Set(Key, [edited, ApprovedSets.Phase("p0001b")],
                ApprovedSets.Approval(ApprovedSets.Noon.AddHours(1))),
            []);

        var decision = _sut.Decide(record, branch, Key)!;

        decision.Set!.Phases[0].Draft.Goal.Should().Be(branch.Set.Phases[0].Draft.Goal,
            "an executed phase is append-only");
        decision.Cause.Should().Contain("p0001a").And.Contain("discarded",
            "the revision the run writes says the edit was not taken");
    }

    [Fact]
    public void Reapproval_Published_KeepsTheBranchAccountingAndClearsItsHandback()
    {
        var branch = Branch(
            ApprovedSets.Noon, ["p0001a"], executed: [],
            accounting: new SpecAccounting([new CarriedSegment(1, "p0001a")], [], []),
            handback: new SpecHandback(SpecHandbackCase.Question, "which reading?"));
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(1));

        var merged = _sut.Decide(record, branch, Key)!.Set!;

        merged.Accounting.Carried.Should().ContainSingle("what a derived predecessor recorded is not erased");
        merged.Handback.Should().BeNull("a hand-back asks a question an approval has answered");
        merged.IsHandedBack.Should().BeFalse();
    }

    [Fact]
    public void ApprovedSet_OverMaxPhases_IsRefusedByTheRunAndNotTruncated()
    {
        var tooMany = Enumerable.Range(0, SpecSet.MaxPhases + 1).Select(i => $"p000{i}").ToList();
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon, tooMany);

        var decision = _sut.Decide(record, branchArtifact: null, Key);

        decision!.Set.Should().BeNull("truncating a ratified set would silently drop approved work");
        decision.Error.Should().Contain(SpecSet.MaxPhases.ToString());
    }

    /// <summary>
    /// 2026-09-17-0e79a review: an old over-cap record left in the store must not fail every
    /// later run of a ticket whose branch already carries a perfectly good set. A record that
    /// does not win cannot refuse the run.
    /// </summary>
    [Fact]
    public void ApprovedSet_OverMaxPhasesButOlderThanTheBranch_DoesNotRefuseTheRun()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a"], executed: ["p0001a"]);
        var tooMany = Enumerable.Range(0, SpecSet.MaxPhases + 1).Select(i => $"p000{i}").ToList();
        var stale = ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(-1), tooMany);

        _sut.Decide(stale, branch, Key).Should().BeNull(
            "the branch artifact stands and the stale record is simply not the source");
    }

    /// <summary>
    /// 2026-09-17-0e79a review: a re-approval that adds nothing past the executed head yields
    /// exactly that head, the sequence reports "nothing left to run", and the run SUCCEEDS having
    /// discarded every edit. Refused for the same reason a shorter one is.
    /// </summary>
    [Fact]
    public void Reapproval_ThatAddsNothingPastTheExecutedHead_IsRefusedInsteadOfSucceedingEmpty()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a", "p0001b"], executed: ["p0001a", "p0001b"]);
        var edited = new SpecApprovalRecord(
            Key,
            ApprovedSets.Set(
                Key,
                [ApprovedSets.Phase("p0001a", "A different goal"), ApprovedSets.Phase("p0001b")],
                ApprovedSets.Approval(ApprovedSets.Noon.AddHours(1))),
            [],
            ApprovedSets.Tracker);

        var decision = _sut.Decide(edited, branch, Key);

        decision!.Set.Should().BeNull();
        decision.Error.Should().Contain("adds no phase past")
            .And.Contain("p0001a", "the run names the edit it will not silently swallow");
    }

    /// <summary>
    /// The same shape with NO edit is a re-approval of finished work: it is allowed through and
    /// the sequence's own "nothing left to run" is the honest answer.
    /// </summary>
    [Fact]
    public void Reapproval_IdenticalToAFinishedSet_IsNotRefused()
    {
        var branch = Branch(ApprovedSets.Noon, ["p0001a"], executed: ["p0001a"]);
        var same = new SpecApprovalRecord(
            Key,
            ApprovedSets.Set(
                Key, [.. branch.Set.Phases], ApprovedSets.Approval(ApprovedSets.Noon.AddHours(1))),
            [],
            ApprovedSets.Tracker);

        _sut.Decide(same, branch, Key)!.Error.Should().BeNull();
    }

    /// <summary>
    /// 2026-09-17-0e79a review: ExecutedHead is the CONTIGUOUS prefix while Executed is copied
    /// wholesale, so a hand-edited set.yaml naming a non-contiguous executed phase would let a
    /// shorter re-approval through the head-only guard.
    /// </summary>
    [Fact]
    public void Reapproval_ShorterThanANonContiguousExecutedList_IsStillRefused()
    {
        // p0001b executed, p0001a did not: the head is empty, but two phases have run.
        var branch = Branch(ApprovedSets.Noon, ["p0001a", "p0001b"], executed: ["p0001b"]);
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(1), []);

        var decision = _sut.Decide(record, branch, Key);

        decision!.Set.Should().BeNull("an executed phase is append-only however set.yaml lists it");
        decision.Error.Should().Contain("append-only");
    }

    [Fact]
    public void ApprovedSetMerge_NoBranchYet_IsTheApprovedSetItself()
    {
        var approved = ApprovedSets.Set(
            Key, [ApprovedSets.Phase("p0001a")], ApprovedSets.Approval(ApprovedSets.Noon));

        var merged = ApprovedSetMerge.Over(approved, branch: null);

        merged.Error.Should().BeNull();
        merged.Set!.Source.Should().Be(SpecSource.Approved);
        merged.Note.Should().BeNull();
    }

    /// <summary>set.yaml is what the precedence compares, so it must read back what it wrote.</summary>
    [Fact]
    public void SetYaml_TheApproval_RoundTripsThroughTheOneSerializer()
    {
        var index = new SpecSetIndex();
        var set = ApprovedSets.Set(
            Key, [ApprovedSets.Phase("p0001a")], ApprovedSets.Approval(ApprovedSets.Noon, "session-9"))
            with { Revisions = [new SpecRevision(1, "initial derivation", ApprovedSets.Noon)] };

        var read = index.ApprovalOf(index.Parse(index.Serialize(set))!);

        read.Should().Be(set.Approval);
    }

    [Fact]
    public void SetYaml_ASetNobodyApproved_ReadsBackWithNoApproval()
    {
        var index = new SpecSetIndex();
        var set = ApprovedSets.Set(Key, [ApprovedSets.Phase("p0001a")], approval: null, SpecSource.Derived)
            with { Revisions = [new SpecRevision(1, "initial derivation", ApprovedSets.Noon)] };

        index.ApprovalOf(index.Parse(index.Serialize(set))!).Should().BeNull();
    }

    [Fact]
    public void SpecMarkdown_ApprovedSource_NamesTheConversation()
    {
        var set = ApprovedSets.Set(
            Key, [ApprovedSets.Phase("p0001a")], ApprovedSets.Approval(ApprovedSets.Noon, "session-42"))
            with { Revisions = [new SpecRevision(1, "approved in design conversation session-42", ApprovedSets.Noon)] };

        SpecMarkdown.Render(set).Should().Contain("approved in design conversation session-42")
            .And.NotContain("derived from the ticket");
    }

    private static SpecSetReadResult Branch(
        DateTimeOffset approvedAt, IReadOnlyList<string> phaseIds, IReadOnlyList<string> executed,
        SpecAccounting? accounting = null, SpecHandback? handback = null) =>
        new(
            new SpecSet(
                Key,
                [.. phaseIds.Select(id => ApprovedSets.Phase(id, $"Goal {id} as it ran"))],
                accounting ?? SpecAccounting.Empty,
                [new SpecRevision(1, "initial derivation", ApprovedSets.Noon)],
                SpecSource.BranchArtifact,
                handback,
                ExecutedPhaseIds: executed,
                Approval: ApprovedSets.Approval(approvedAt)),
            "branch-sha");
}
