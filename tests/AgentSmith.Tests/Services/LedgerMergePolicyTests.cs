using System.Collections.Generic;
using System.Linq;
using AgentSmith.Contracts.Progress;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0368: the pure ledger-merge policy. A step that is DONE stays DONE — a rewrite
// may restructure PENDING work freely but may not silently drop (omit) or revert
// a completed item; only an explicit reopen signal releases a done step. Matching
// prefers the stable id and falls back to normalized activity + target so a
// reworded rewrite still recognises the same completed step.
public sealed class LedgerMergePolicyTests
{
    private static readonly IReadOnlySet<string> NoReopens = new HashSet<string>();

    private static ProgressLedger Ledger(params ProgressLedgerEntry[] entries) => new(entries);

    // Distinct default activity per id so the activity+target fallback key never
    // collides across unrelated steps (real ledger steps have distinct descriptions).
    private static ProgressLedgerEntry Done(string id, string? activity = null, string? target = null)
        => new(id, activity ?? $"step {id}", ProgressStatus.Done, target);

    private static ProgressLedgerEntry Pending(string id, string? activity = null, string? target = null)
        => new(id, activity ?? $"step {id}", ProgressStatus.Pending, target);

    [Fact]
    public void RewriteToNewStructure_KeepsPreviouslyDoneItemsDone()
    {
        var retained = Ledger(Done("1"), Done("2"), Pending("3"));
        // A completely different plan shape — new pending ids, done ids omitted.
        var incoming = Ledger(Pending("a"), Pending("b"));

        var result = LedgerMergePolicy.Merge(retained, incoming, NoReopens);

        result.Merged.Entries.Where(e => e.Status == ProgressStatus.Done)
            .Select(e => e.Id).Should().BeEquivalentTo("1", "2");
        result.Merged.Entries.Select(e => e.Id).Should().Contain(new[] { "a", "b" });
        result.ReattachedDone.Should().Be(2);
    }

    [Fact]
    public void RewriteOmittingDoneItem_ReattachesAndCountsIt()
    {
        var retained = Ledger(Done("1"), Pending("2"));
        var incoming = Ledger(Done("2"));

        var result = LedgerMergePolicy.Merge(retained, incoming, NoReopens);

        result.Merged.Entries.Select(e => e.Id).Should().BeEquivalentTo("2", "1");
        result.Merged.Entries.Single(e => e.Id == "1").Status.Should().Be(ProgressStatus.Done);
        result.ReattachedDone.Should().Be(1);
    }

    [Fact]
    public void RewriteRegressingDoneToPending_Rejected_StaysDone()
    {
        var retained = Ledger(Done("1"));
        var incoming = Ledger(Pending("1"));

        var result = LedgerMergePolicy.Merge(retained, incoming, NoReopens);

        result.Merged.Entries.Single().Status.Should().Be(ProgressStatus.Done);
        result.RejectedRegressions.Should().Be(1);
        result.ReattachedDone.Should().Be(0);
    }

    [Fact]
    public void ExplicitReopen_ReleasesDoneItemToPending()
    {
        var retained = Ledger(Done("1"));
        var incoming = Ledger(Pending("1"));
        var reopens = new HashSet<string> { "1" };

        var result = LedgerMergePolicy.Merge(retained, incoming, reopens);

        result.Merged.Entries.Single().Status.Should().Be(ProgressStatus.Pending);
        result.ExplicitReverts.Should().Be(1);
        result.RejectedRegressions.Should().Be(0);
    }

    [Fact]
    public void PendingItems_FreelyAddedRemovedReordered()
    {
        var retained = Ledger(Done("1"), Pending("2"), Pending("3"));
        // Drop pending 2, add pending 4, reorder, flip 3 to in_progress.
        var incoming = Ledger(
            new ProgressLedgerEntry("4", "step 4", ProgressStatus.Pending),
            new ProgressLedgerEntry("3", "step 3", ProgressStatus.InProgress),
            new ProgressLedgerEntry("1", "step 1", ProgressStatus.Done));

        var result = LedgerMergePolicy.Merge(retained, incoming, NoReopens);

        result.Merged.Entries.Select(e => e.Id).Should().BeEquivalentTo("4", "3", "1");
        result.Merged.Entries.Single(e => e.Id == "3").Status.Should().Be(ProgressStatus.InProgress);
        result.ReattachedDone.Should().Be(0);
        result.RejectedRegressions.Should().Be(0);
    }

    [Fact]
    public void DoneItem_MatchedByActivityAndTarget_WhenIdChanges()
    {
        // A reworded rewrite gives the same completed step a new id but keeps its
        // activity + target — the fallback key still recognises it and holds it done.
        var retained = Ledger(Done("1", "build the widget", "src/Widget.cs"));
        var incoming = Ledger(
            new ProgressLedgerEntry("99", "build the widget", ProgressStatus.Pending, "src/Widget.cs"));

        var result = LedgerMergePolicy.Merge(retained, incoming, NoReopens);

        result.Merged.Entries.Should().ContainSingle();
        result.Merged.Entries.Single().Status.Should().Be(ProgressStatus.Done);
        result.RejectedRegressions.Should().Be(1);
        result.ReattachedDone.Should().Be(0, "the fallback match consumed the retained done item");
    }

    [Fact]
    public void NewIncomingDoneItems_AddToTheDoneCount()
    {
        var retained = Ledger(Done("1"));
        var incoming = Ledger(Done("1"), Done("2"));

        var result = LedgerMergePolicy.Merge(retained, incoming, NoReopens);

        result.Merged.Entries.Count(e => e.Status == ProgressStatus.Done).Should().Be(2);
        result.ReattachedDone.Should().Be(0);
    }
}
