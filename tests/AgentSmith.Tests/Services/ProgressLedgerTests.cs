using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0341: the durable progress ledger. These pin the invariants that make a
// full-state-replace safe as MEMORY (the holes we found reviewing the spec):
// at-most-one in_progress, no silent drop of a seeded/done step (reconcile-by-id),
// a size cap, an EXPLICIT target for the honesty diagnostic (no fuzzy matching),
// and that the ledger never touches p0340's keystone.
public sealed class ProgressLedgerTests
{
    private static ProgressUpdateItem Item(string id, string status, string? target = null, string? note = null)
        => new(id, $"activity {id}", status, target, note);

    [Fact]
    public async Task ProgressLedger_SecondUpdate_FullyReplacesPriorItems()
    {
        var host = new ProgressLedgerToolHost();
        await host.UpdateProgress(new[] { Item("1", "pending"), Item("2", "pending") });

        await host.UpdateProgress(new[] { Item("1", "done"), Item("2", "done"), Item("3", "in_progress") });

        var ledger = host.GetLedger();
        ledger.Entries.Should().HaveCount(3);
        ledger.Entries.Select(e => e.Id).Should().ContainInOrder("1", "2", "3");
        ledger.Entries.Single(e => e.Id == "3").Status.Should().Be(ProgressStatus.InProgress);
    }

    [Fact]
    public void ActionablePending_ReturnsPendingAndInProgress_NotDone()
    {
        // p0341c: the re-engagement signal + a keystone input — pending|in_progress.
        var ledger = new ProgressLedger(new[]
        {
            new ProgressLedgerEntry("1", "a", ProgressStatus.Done),
            new ProgressLedgerEntry("2", "b", ProgressStatus.InProgress),
            new ProgressLedgerEntry("3", "c", ProgressStatus.Pending),
        });

        ledger.HasActionablePending.Should().BeTrue();
        ledger.ActionablePending.Select(e => e.Id).Should().BeEquivalentTo("2", "3");
    }

    [Fact]
    public void ActionablePending_AllDone_Drained()
    {
        var ledger = new ProgressLedger(new[]
        {
            new ProgressLedgerEntry("1", "a", ProgressStatus.Done),
            new ProgressLedgerEntry("2", "b", ProgressStatus.Done),
        });

        ledger.HasActionablePending.Should().BeFalse();
        ledger.ActionablePending.Should().BeEmpty();
    }

    [Fact]
    public async Task ProgressLedger_TwoInProgress_RejectedWithClearError()
    {
        var host = new ProgressLedgerToolHost();

        var result = await host.UpdateProgress(new[] { Item("1", "in_progress"), Item("2", "in_progress") });

        result.Should().Contain("at most one item may be in_progress");
        host.GetLedger().IsEmpty.Should().BeTrue("a rejected update must not mutate the store");
    }

    [Fact]
    public async Task ProgressLedger_ReplaceDroppingDoneItem_Rejected()
    {
        var host = new ProgressLedgerToolHost();
        await host.UpdateProgress(new[] { Item("1", "done"), Item("2", "pending") });

        // A full replace that silently loses the already-done step 1.
        var result = await host.UpdateProgress(new[] { Item("2", "done") });

        result.Should().Contain("may not DROP");
        result.Should().Contain("1");
        host.GetLedger().Entries.Should().Contain(e => e.Id == "1", "the rejected drop must not take effect");
    }

    [Fact]
    public async Task ProgressLedger_ModelReusesSeedIds_LifecycleTrackedById()
    {
        var seed = ProgressLedgerSeeder.Seed(PlanWith(("build the thing", "src/Thing.cs")));
        var host = new ProgressLedgerToolHost(seed);

        // Model flips the SAME seeded id (1) through its lifecycle — reconcile-by-id.
        await host.UpdateProgress(new[] { Item("1", "in_progress", "src/Thing.cs") });
        await host.UpdateProgress(new[] { Item("1", "done", "src/Thing.cs") });

        var entry = host.GetLedger().Entries.Single();
        entry.Id.Should().Be("1");
        entry.Status.Should().Be(ProgressStatus.Done);
    }

    [Fact]
    public async Task ProgressLedger_DoneFlippedBackToPending_AcceptedForDecisionRevision()
    {
        // p0356: decide-once-then-fan-out — a REVISED convention flips affected
        // done items back to pending. The reconcile protects ids from being
        // DROPPED, never from honest status regression.
        var host = new ProgressLedgerToolHost();
        await host.UpdateProgress(new[] { Item("1", "done"), Item("2", "done") });

        var result = await host.UpdateProgress(new[]
        {
            Item("1", "pending", note: "decision revised — re-apply new convention"),
            Item("2", "done"),
        });

        result.Should().NotContain("Error");
        host.GetLedger().Entries.Single(e => e.Id == "1").Status.Should().Be(ProgressStatus.Pending);
    }

    [Fact]
    public async Task ProgressLedger_OverMaxItems_RejectedWithClearError()
    {
        var host = new ProgressLedgerToolHost();
        var tooMany = Enumerable.Range(1, ProgressLedger.MaxItems + 1)
            .Select(i => Item(i.ToString(), "pending")).ToArray();

        var result = await host.UpdateProgress(tooMany);

        result.Should().Contain("cap");
        host.GetLedger().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void LedgerSeed_FromRatifiedPlan_MirrorsStepsAsPendingWithStableIdsAndTargets()
    {
        var seed = ProgressLedgerSeeder.Seed(PlanWith(("step one", "src/A.cs"), ("step two", null)));

        seed.Should().HaveCount(2);
        seed[0].Id.Should().Be("1");
        seed[0].Status.Should().Be(ProgressStatus.Pending);
        seed[0].Target.Should().Be("src/A.cs");
        seed[1].Id.Should().Be("2");
        seed[1].Target.Should().BeNull();
    }

    [Fact]
    public void LedgerSeed_NoPlan_StartsEmptyAndDoesNotThrow()
    {
        ProgressLedgerSeeder.Seed(null).Should().BeEmpty();
        new ProgressLedgerToolHost(ProgressLedgerSeeder.Seed(null)).GetLedger().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void DoneCheck_TargetPresentInDiff_NoWarning()
    {
        var ledger = new ProgressLedger(new[]
        {
            new ProgressLedgerEntry("1", "edit A", ProgressStatus.Done, Target: "src/A.cs"),
        });
        var changes = new[] { Change("primary/src/A.cs") };

        ProgressLedgerCoverage.UnbackedDoneSteps(ledger, changes).Should().BeEmpty();
    }

    [Fact]
    public void DoneCheck_DoneItemWithNoTarget_SkippedNotFalseWarned()
    {
        var ledger = new ProgressLedger(new[]
        {
            new ProgressLedgerEntry("1", "some free-text activity", ProgressStatus.Done, Target: null),
        });

        ProgressLedgerCoverage.UnbackedDoneSteps(ledger, Array.Empty<CodeChange>())
            .Should().BeEmpty("a done item with no target is skipped, never a false warning");
    }

    [Fact]
    public void DoneCheck_TargetAbsentFromDiff_SurfacedAsWarning()
    {
        var ledger = new ProgressLedger(new[]
        {
            new ProgressLedgerEntry("2", "migrate B", ProgressStatus.Done, Target: "src/B.cs"),
        });
        var changes = new[] { Change("primary/src/A.cs") };

        var warnings = ProgressLedgerCoverage.UnbackedDoneSteps(ledger, changes);
        warnings.Should().ContainSingle();
        warnings[0].Should().Contain("src/B.cs");
    }

    [Fact]
    public void Keystone_Unchanged_LedgerWarningDoesNotAlterVerdict()
    {
        // p0341 explicitly does NOT touch RunOutcomeKeystone — the ledger is not even
        // a parameter, so no ledger state can alter the verdict. This guards the
        // no-scope-leak-into-p0340 contract: the p0340 signature still governs alone.
        var green = new MasterVerification(VerificationStatus.Green, true, true, true, true, "ok");
        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: green,
            ratifiedCriteria: Array.Empty<string>());

        verdict.Satisfied.Should().BeTrue();
    }

    private static Plan PlanWith(params (string Description, string? Target)[] steps)
    {
        var planSteps = steps.Select((s, i) =>
            new PlanStep(i + 1, s.Description, s.Target is null ? null : new FilePath(s.Target), "Modify")).ToList();
        return new Plan("summary", planSteps, "{}");
    }

    private static CodeChange Change(string path) => new(new FilePath(path), "content", "Modify");
}
