using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0391: the ledger-turnover bound. Since p0374 the checklist is fully model-owned with no
/// turnover limit, and ShouldReengage re-drives on any actionable item — so a model that closes
/// its checklist and appends "verify X once more" re-drives itself indefinitely. The stop must
/// be mechanical (the observed 124-minute run happened despite a long, careful prompt) and must
/// NOT fire on a legitimately productive pass. These pin both halves.
/// </summary>
public sealed class ReengageLedgerTurnoverTests
{
    private static ProgressLedger Ledger(params (string Id, ProgressStatus Status)[] items) =>
        new(items.Select(i => new ProgressLedgerEntry(i.Id, $"step {i.Id}", i.Status)).ToList());

    private static IReadOnlyList<CodeChange> Changes(params (string Path, string Content)[] writes) =>
        writes.Select(w => new CodeChange(new FilePath(w.Path), w.Content, "modified")).ToList();

    private static readonly IReadOnlyCollection<string> NoIds = new HashSet<string>();

    // ---- the failure this exists to stop ----

    [Fact]
    public void IsSelfRefilled_PassClosesOldStepsAndInventsNewOnesWithoutWriting_True()
    {
        // The observed shape: the real work is finished, the pass marks the remaining step done
        // and appends a fresh re-verification item. No id survived, nothing reached the diff.
        var before = new HashSet<string> { "1", "2" };
        var after = Ledger(("1", ProgressStatus.Done), ("2", ProgressStatus.Done),
            ("re-verify-1", ProgressStatus.Pending));

        ReengageProgressPolicy.IsSelfRefilled(before, after, producedNewWork: false)
            .Should().BeTrue();
    }

    [Fact]
    public void Decide_SelfRefilledPass_StopsWithItsOwnReason()
    {
        // Surfaced as its own outcome, like StopIdle / StopBlocked — never a silent truncation.
        ReengageProgressPolicy.Decide(
            toolCallsInPass: 6, block: null, passEndedOnException: false, ledgerSelfRefilled: true)
            .Should().Be(ReengageOutcome.StopSelfRefilled);
    }

    // ---- it must not fire on a productive pass ----

    [Fact]
    public void IsSelfRefilled_PassInventedStepsButAlsoWroteCode_False()
    {
        // Restructuring the plan WHILE doing the work is normal execution, not circling.
        var before = new HashSet<string> { "1" };
        var after = Ledger(("split-a", ProgressStatus.Done), ("split-b", ProgressStatus.InProgress));

        ReengageProgressPolicy.IsSelfRefilled(before, after, producedNewWork: true)
            .Should().BeFalse();
    }

    [Fact]
    public void IsSelfRefilled_OriginalStepStillPending_False()
    {
        // The ordinary "work the checklist" shape: a step from the seeded plan is still open,
        // so the model is not refilling — it is executing. A verify-only pass lands here too.
        var before = new HashSet<string> { "1", "2" };
        var after = Ledger(("1", ProgressStatus.Done), ("2", ProgressStatus.InProgress),
            ("3", ProgressStatus.Pending));

        ReengageProgressPolicy.IsSelfRefilled(before, after, producedNewWork: false)
            .Should().BeFalse();
    }

    [Fact]
    public void IsSelfRefilled_DrainedLedger_False()
    {
        // A fully-drained ledger is the ordinary completion path — ShouldReengage owns it, and
        // this bound must not claim the stop (or the run trail would name the wrong reason).
        ReengageProgressPolicy.IsSelfRefilled(
            new HashSet<string> { "1" }, Ledger(("1", ProgressStatus.Done)), producedNewWork: false)
            .Should().BeFalse();
    }

    [Fact]
    public void IsSelfRefilled_FirstPassOverAnEmptyStartingLedger_TrueOnlyWhenNothingWasWritten()
    {
        // A model that seeds a checklist and immediately starts editing is productive…
        ReengageProgressPolicy.IsSelfRefilled(
            NoIds, Ledger(("1", ProgressStatus.InProgress)), producedNewWork: true)
            .Should().BeFalse();
        // …one that seeds a checklist and writes nothing has produced only a checklist.
        ReengageProgressPolicy.IsSelfRefilled(
            NoIds, Ledger(("1", ProgressStatus.Pending)), producedNewWork: false)
            .Should().BeTrue();
    }

    // ---- the diff half: the tool host's write log, not the model's claim ----

    [Fact]
    public void ProducedNewWork_PassWroteANewFile_True() =>
        ReengageProgressPolicy.ProducedNewWork(
            Changes(("src/A.cs", "one")),
            Changes(("src/A.cs", "one"), ("src/B.cs", "two")))
            .Should().BeTrue();

    [Fact]
    public void ProducedNewWork_PassRewroteAnExistingFile_True() =>
        // A re-edit keeps the entry count flat — compare content, not just the path set.
        ReengageProgressPolicy.ProducedNewWork(
            Changes(("src/A.cs", "one")), Changes(("src/A.cs", "one, fixed")))
            .Should().BeTrue();

    [Fact]
    public void ProducedNewWork_PassOnlyReadAndRanCommands_False() =>
        ReengageProgressPolicy.ProducedNewWork(
            Changes(("src/A.cs", "one")), Changes(("src/A.cs", "one")))
            .Should().BeFalse();

    // ---- trajectory: the run ends on its own instead of on the operator ----

    [Fact]
    public void Reengage_LedgerRefillLoop_TerminatesInsteadOfCirclingUntilTheBudget()
    {
        // Replays the 2026-07-29 shape: work finished, each pass closes the last item and
        // appends another re-verification one, tools fire every pass (so the p0365 idle gate
        // never trips), nothing is written. Before p0391 this ran until the money ran out.
        var ledgerIds = new HashSet<string> { "1" };
        var passes = 0;
        ReengageOutcome outcome;
        do
        {
            passes++;
            var refilled = Ledger(("1", ProgressStatus.Done), ($"recheck-{passes}", ProgressStatus.Pending));
            outcome = ReengageProgressPolicy.Decide(
                toolCallsInPass: 4, block: null, passEndedOnException: false,
                ledgerSelfRefilled: ReengageProgressPolicy.IsSelfRefilled(
                    ledgerIds, refilled, producedNewWork: false));
            ledgerIds = refilled.Entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        }
        while (outcome == ReengageOutcome.Continue && passes < 50);

        outcome.Should().Be(ReengageOutcome.StopSelfRefilled);
        passes.Should().Be(1, "the very first refilled pass ends the loop — no budget is burned circling");
    }
}
