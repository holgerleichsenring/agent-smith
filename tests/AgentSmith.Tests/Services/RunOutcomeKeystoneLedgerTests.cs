using AgentSmith.Application.Services;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0341c: the keystone cross-checks a self-reported Met against the progress ledger + the
// diff, so a run TRUNCATED by early-stop (marking untouched criteria Met) cannot ship
// green. Downgrade is STRICTLY on Met — a justified NotApplicable is never downgraded for a
// pending step (a not-applicable criterion SHOULD leave its target untouched). Empty-ledger
// runs are byte-for-byte p0340.
public sealed class RunOutcomeKeystoneLedgerTests
{
    private static readonly string[] OneCriterion = { "All MediatR usages replaced by Mediator" };

    private static MasterVerification GreenMet() =>
        new(VerificationStatus.Green, true, true, true, true, "ok",
            AcceptanceDispositions: new[]
            {
                new AcceptanceDisposition(OneCriterion[0], AcceptanceStatus.Met, "swapped"),
            });

    private static MasterVerification GreenNotApplicable() =>
        new(VerificationStatus.Green, true, true, true, true, "ok",
            AcceptanceDispositions: new[]
            {
                new AcceptanceDisposition(OneCriterion[0], AcceptanceStatus.NotApplicable,
                    "no MediatR present — nothing to migrate"),
            });

    private static ProgressLedger Ledger(params ProgressLedgerEntry[] entries) => new(entries);

    private static KeystoneVerdict Evaluate(
        MasterVerification v, ProgressLedger ledger, params string[] changedPaths) =>
        RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: v,
            ratifiedCriteria: OneCriterion, ledger: ledger, changedPaths: changedPaths);

    [Fact]
    public void Keystone_MetButLedgerStepPending_Downgraded()
    {
        var ledger = Ledger(
            new ProgressLedgerEntry("1", "swap DI", ProgressStatus.Done, "src/Di.cs"),
            new ProgressLedgerEntry("2", "swap handlers", ProgressStatus.Pending, "src/Handler.cs"));

        var verdict = Evaluate(GreenMet(), ledger, "src/Di.cs");

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("truncated");
    }

    [Fact]
    public void Keystone_DoneStepTargetNotInDiff_NoLongerPoliced_Ok()
    {
        // p0373: the keystone no longer polices the master's PLAN per step. A done
        // step whose declared target is absent from the diff is fine as long as the
        // run committed real work — which step writes vs. inspects is not the gate's
        // concern. (The old p0341c per-step rule would have FAILED this.)
        var ledger = Ledger(new ProgressLedgerEntry("1", "swap DI", ProgressStatus.Done, "src/Di.cs"));

        var verdict = Evaluate(GreenMet(), ledger, "src/Unrelated.cs");

        verdict.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Keystone_MetTargetLessStepNotDoneOrNoScopeDiff_Downgraded_NoAutoPass()
    {
        // A target-less DONE step with NO diff at all — the Met claim is hollow.
        var ledger = Ledger(new ProgressLedgerEntry("1", "sweeping refactor", ProgressStatus.Done, Target: null));

        var verdict = Evaluate(GreenMet(), ledger /* no changed paths */);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("no code change");
    }

    [Fact]
    public void Keystone_MetTargetLessStepDoneWithScopeDiff_Ok()
    {
        var ledger = Ledger(new ProgressLedgerEntry("1", "sweeping refactor", ProgressStatus.Done, Target: null));

        var verdict = Evaluate(GreenMet(), ledger, "src/Something.cs");

        verdict.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Keystone_NotApplicableWithEvidencePendingStep_NotDowngraded_NoFalseRed()
    {
        // No Met claim — a justified-N/A run must NOT be downgraded for a pending step.
        var ledger = Ledger(new ProgressLedgerEntry("1", "not needed", ProgressStatus.Pending, "src/X.cs"));

        var verdict = Evaluate(GreenNotApplicable(), ledger);

        verdict.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Keystone_EmptyLedger_BehaviourUnchanged_FallsThroughToP0340()
    {
        // Empty ledger => the acceptance gate is exactly p0340: all-Met passes.
        var verdict = Evaluate(GreenMet(), ProgressLedger.Empty);

        verdict.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Keystone_InventoryStepTargetAbsentFromDiff_NotFailed_Regression()
    {
        // p0373 regression guard: the exact run that FAILED wrongly — a repo
        // migration whose ledger held an "Inventory … usage" analysis step whose
        // repo-name target never appears among the SUBPROJECT file paths in the
        // diff. The old verb-whitelist read-only exemption did not recognise
        // "Inventory" and killed a perfect migration. There is real delivery, so
        // the keystone must pass: the plan's per-step targets are not policed.
        var ledger = Ledger(
            new ProgressLedgerEntry("1", "Inventory MediatR and MassTransit usage across solutions",
                ProgressStatus.Done, "PortalServer"),
            new ProgressLedgerEntry("2", "Migrate handlers to Mediator",
                ProgressStatus.Done, "PortalApi/Handler.cs"));

        var verdict = Evaluate(GreenMet(), ledger, "PortalApi/Handler.cs");

        verdict.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Keystone_AllStepsDoneAndBacked_MetSucceeds()
    {
        var ledger = Ledger(
            new ProgressLedgerEntry("1", "swap DI", ProgressStatus.Done, "src/Di.cs"),
            new ProgressLedgerEntry("2", "swap handlers", ProgressStatus.Done, "src/Handler.cs"));

        var verdict = Evaluate(GreenMet(), ledger, "src/Di.cs", "src/Handler.cs");

        verdict.Satisfied.Should().BeTrue();
    }
}
