using System;
using System.Collections.Generic;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0400: a phase may declare ships_code: false — it ships knowledge (inventory,
// classification), by design without a source diff. The keystone then judges it
// by its done criteria: the no-diff and hollow-delivery rules stand down. The
// p0384 per-repo expected-changes gate is deliberately NOT relaxed — an
// exploration phase may ship nothing, the run's must-change repos may not.
public sealed class RunOutcomeKeystoneShipsCodeTests
{
    private static readonly string[] Criteria = { "Every branch is classified and documented" };

    private static MasterVerification GreenMet() =>
        new(VerificationStatus.Green, true, true, true, true, "ok",
            AcceptanceDispositions: new[]
            {
                new AcceptanceDisposition(Criteria[0], AcceptanceStatus.Met, "inventory delivered"),
            });

    private static ProgressLedger DrainedLedger() =>
        new(new[] { new ProgressLedgerEntry("1", "classify branches", ProgressStatus.Done, null) });

    private static KeystoneVerdict Evaluate(
        bool shipsCode,
        IReadOnlyDictionary<string, bool>? perRepo = null,
        IReadOnlyList<string>? expectedChangeRepos = null) =>
        RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: false, recordedChange: false,
            verification: GreenMet(), ratifiedCriteria: Criteria,
            ledger: DrainedLedger(), changedPaths: Array.Empty<string>(),
            perRepoCommittedChange: perRepo, expectedChangeRepos: expectedChangeRepos,
            shipsCode: shipsCode);

    [Fact]
    public void PhaseKeystone_ShipsCodeFalse_DoneCriteriaMet_NoDiff_Succeeds() =>
        Evaluate(shipsCode: false).Satisfied.Should().BeTrue(
            "a knowledge phase with all done criteria met is a success without a diff");

    [Fact]
    public void PhaseKeystone_ShipsCodeTrue_NoDiff_StillFails()
    {
        var verdict = Evaluate(shipsCode: true);

        verdict.Satisfied.Should().BeFalse("a code phase without a diff has nothing to ship");
        verdict.FailureReason.Should().Contain("no code changes");
    }

    [Fact]
    public void PhaseKeystone_ShipsCodeFalse_UnmetCriterion_StillFails()
    {
        var verification = new MasterVerification(
            VerificationStatus.Green, true, true, true, true, "ok",
            AcceptanceDispositions: new[]
            {
                new AcceptanceDisposition(Criteria[0], AcceptanceStatus.Unmet, ""),
            });

        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: false, recordedChange: false,
            verification: verification, ratifiedCriteria: Criteria,
            ledger: DrainedLedger(), changedPaths: Array.Empty<string>(),
            shipsCode: false);

        verdict.Satisfied.Should().BeFalse(
            "ships_code: false is judged BY the done criteria — an unmet criterion is still red");
    }

    [Fact]
    public void RunKeystone_ExpectedChangesRepos_StillRequireDiffs()
    {
        // The run-level honesty net (p0384) is untouched by the phase's declaration:
        // a repo the scope classifier expects to change must show a committed diff.
        var verdict = Evaluate(
            shipsCode: false,
            perRepo: new Dictionary<string, bool> { ["server"] = false },
            expectedChangeRepos: new[] { "server" });

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("server");
    }
}
