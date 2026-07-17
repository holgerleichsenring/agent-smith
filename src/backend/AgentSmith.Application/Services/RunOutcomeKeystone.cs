using System;
using System.Collections.Generic;
using System.Linq;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0241 keystone: the single framework rule that a fix/feature run may NOT be
/// reported as success unless it actually changed code AND its verification is
/// green. Pure and deterministic — the same inputs always yield the same
/// verdict, so it is the regression gate the last ~40 phases lacked.
///
/// Three signals, by design of different trust levels:
///  - <paramref name="gitCommittedChange"/> is git-diff truth — a real change
///    actually staged/committed in the repo working tree (what ships).
///  - <paramref name="recordedChange"/> is what the agent's tool-writes recorded.
///    p0244: when the agent RECORDED edits but git committed NOTHING, the edits
///    never reached the working tree (wrong path / not staged) — a loud failure,
///    NOT a success. Crediting recordedChange alone used to mask exactly this.
///  - <paramref name="verification"/> is the master's self-report (auditable;
///    a lying model is a model-fitness problem caught by a metered run, not a
///    framework problem). The framework only refuses to call an unverified or
///    self-declared-red run a success.
/// </summary>
public static class RunOutcomeKeystone
{
    public static KeystoneVerdict Evaluate(
        bool expectsCodeChanges,
        bool expectsGreenTests,
        bool gitCommittedChange,
        bool recordedChange,
        MasterVerification? verification,
        IReadOnlyList<string> ratifiedCriteria,
        // p0341c: the progress ledger + the run's changed paths are new DETERMINISTIC
        // inputs so a truncated run cannot self-report green. Null/empty ledger => the
        // acceptance gate is byte-for-byte p0340 (no-contract / no-ledger runs unchanged).
        ProgressLedger? ledger = null,
        IReadOnlyList<string>? changedPaths = null)
    {
        if (expectsCodeChanges && !gitCommittedChange)
        {
            // p0244: distinguish "agent did nothing" from "agent's edits never
            // landed in the repo" — the latter is the write-placement bug that
            // looked like a hollow success while result.md claimed changes.
            if (recordedChange)
                return KeystoneVerdict.Fail(
                    "The agent recorded source edits but git committed NOTHING — the writes "
                    + "never reached the repo working tree (wrong path, or not staged). "
                    + "result.md may claim changes that were never shipped. Recorded as FAILED.");
            return KeystoneVerdict.Fail(
                "This fix/feature run produced no code changes — the agent investigated "
                + "but never modified any source, so there is nothing to ship. Recorded as "
                + "FAILED; the agent's reasoning is in result.md / the record PR.");
        }

        if (expectsGreenTests)
        {
            var gate = EvaluateVerificationGate(verification);
            if (!gate.Satisfied) return gate;
        }

        // p0340: the change built and tested green — but that alone was the hole
        // (a 1-line edit against a whole-migration contract shipped as success).
        // The real definition of done is the ratified acceptance contract.
        // p0341c: the ledger + diff cross-check catches a run truncated by early-stop
        // that self-reported Met on untouched criteria.
        return EvaluateAcceptance(ratifiedCriteria, verification, ledger, changedPaths);
    }

    private static KeystoneVerdict EvaluateVerificationGate(MasterVerification? verification)
    {
        if (verification is null)
            return KeystoneVerdict.Fail(
                "The coding agent did not emit a verification verdict, so its build/test "
                + "outcome is unknown — a run with an unknown outcome cannot be reported as "
                + "success. Ensure the verdict-emitting coding skills are pinned in "
                + "agentsmith.yml.");

        if (verification.Status == VerificationStatus.Unknown)
            return KeystoneVerdict.Fail(
                "The coding agent's verification verdict was unparseable / incomplete, "
                + "so the run's build/test outcome is unknown and cannot be a success.");

        // A broken build is never shippable, regardless of the test diff.
        if (verification.BuildRan && !verification.BuildPassed)
            return KeystoneVerdict.Fail("The build did not pass — run recorded as failed.");

        return EvaluateTests(verification);
    }

    // p0340: gate on the ratified acceptance contract — the run's true definition
    // of done. Empty criteria (fix-bug negotiated none, or a non-contract preset)
    // fall back to Ok: the change+green gates above already ruled, so nothing here
    // may tip the no-contract case over. When criteria exist, EVERY one must be
    // met (an edit) or justified not-applicable (an evaluated reason) — a missing
    // disposition or an unmet/unjustified criterion is the honest RED.
    private static KeystoneVerdict EvaluateAcceptance(
        IReadOnlyList<string> criteria, MasterVerification? verification,
        ProgressLedger? ledger, IReadOnlyList<string>? changedPaths)
    {
        if (criteria.Count == 0) return KeystoneVerdict.Ok();

        var dispositions = verification?.AcceptanceDispositions;
        if (dispositions is null || dispositions.Count == 0)
            return KeystoneVerdict.Fail(
                $"The run negotiated {criteria.Count} acceptance criteria but the master emitted no "
                + "per-criterion disposition — its build/test self-report cannot confirm the contract "
                + "was delivered. Recorded as FAILED (each criterion must be reported met or "
                + "justified not-applicable).");

        var unresolved = new List<string>();
        for (var i = 0; i < criteria.Count; i++)
        {
            if (i >= dispositions.Count)
            {
                unresolved.Add($"\"{criteria[i]}\" (no disposition reported)");
                continue;
            }
            var d = dispositions[i];
            if (d.Status == AcceptanceStatus.Met) continue;
            if (d.Status == AcceptanceStatus.NotApplicable && !string.IsNullOrWhiteSpace(d.Evidence)) continue;
            var why = d.Status == AcceptanceStatus.NotApplicable
                ? "marked not-applicable without the required evaluated reason"
                : "not met";
            unresolved.Add($"\"{criteria[i]}\" ({why})");
        }

        if (unresolved.Count > 0)
            return KeystoneVerdict.Fail(
                $"{unresolved.Count} of {criteria.Count} acceptance criteria are unmet or unjustified: "
                + string.Join("; ", unresolved)
                + ". The run did not deliver its acceptance contract — recorded as FAILED.");

        // p0341c: the disposition-level gate passed — now cross-check against the LEDGER
        // so a run TRUNCATED by early-stop (marking untouched criteria Met) cannot ship
        // green. STRICTLY on Met: a run that only justified-N/A its criteria (no Met claim)
        // is never downgraded for a pending step — a not-applicable criterion SHOULD leave
        // its target untouched. Empty ledger => unchanged (falls through to p0340).
        return CrossCheckLedger(dispositions, ledger, changedPaths);
    }

    // p0341c: the deterministic ledger cross-check. Fires only when the master CLAIMED
    // delivery (≥1 Met disposition) and a non-empty ledger is present.
    //  - a Met claim while an ACTIONABLE step (pending/in_progress) remains => truncated.
    //  - a DONE step whose PRESENT target is absent from the diff => unbacked done.
    //  - a TARGET-LESS DONE step with NO diff at all => nothing shipped for it.
    // A justified-N/A-only run (no Met) is never downgraded here.
    private static KeystoneVerdict CrossCheckLedger(
        IReadOnlyList<AcceptanceDisposition> dispositions,
        ProgressLedger? ledger, IReadOnlyList<string>? changedPaths)
    {
        if (ledger is null || ledger.IsEmpty) return KeystoneVerdict.Ok();
        if (!dispositions.Any(d => d.Status == AcceptanceStatus.Met)) return KeystoneVerdict.Ok();

        var actionable = ledger.ActionablePending;
        if (actionable.Count > 0)
            return KeystoneVerdict.Fail(
                $"The master reported the acceptance contract met, but {actionable.Count} plan step(s) "
                + "are still open in the progress ledger — the run was truncated, not completed: "
                + string.Join("; ", actionable.Take(6).Select(e => $"[{e.Id}] {e.Activity}"))
                + ". Recorded as FAILED (a Met claim over unfinished steps is not a success).");

        var paths = (changedPaths ?? Array.Empty<string>())
            .Select(NormalizePath).ToList();
        var anyDiff = paths.Count > 0;
        foreach (var e in ledger.Entries.Where(e => e.Status == ProgressStatus.Done))
        {
            if (!string.IsNullOrWhiteSpace(e.Target))
            {
                if (!TargetInDiff(NormalizePath(e.Target!), paths))
                    return KeystoneVerdict.Fail(
                        $"Ledger step [{e.Id}] \"{e.Activity}\" is marked done and its criterion "
                        + $"reported met, but its target '{e.Target}' is absent from the committed diff. "
                        + "Recorded as FAILED (a done step with no matching change is not delivered).");
            }
            else if (!anyDiff)
            {
                // A target-less done step needs SOME change in scope; with an empty diff the
                // whole Met claim is hollow.
                return KeystoneVerdict.Fail(
                    $"Ledger step [{e.Id}] \"{e.Activity}\" is marked done with the criterion reported "
                    + "met, but the run committed no code change at all. Recorded as FAILED.");
            }
        }

        return KeystoneVerdict.Ok();
    }

    // Same equal-or-endswith rule ProgressLedgerCoverage uses, kept local so the keystone
    // stays a self-contained pure function.
    private static bool TargetInDiff(string target, IReadOnlyList<string> paths) =>
        paths.Any(p =>
            string.Equals(p, target, StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("/" + target, StringComparison.OrdinalIgnoreCase));

    private static string NormalizePath(string p) => p.Replace('\\', '/').TrimStart('/');

    // p0273: gate on REGRESSIONS, not on any red. When the agent reported the raw
    // failing-test lists, the framework computes new-failures = final \ baseline —
    // a test already red at HEAD does not fail the run; only green→red does, and
    // "unrelated" is measured here, never asserted by the agent. When the lists are
    // absent (older skill) the gate falls back to the original binary behaviour.
    private static KeystoneVerdict EvaluateTests(MasterVerification verification)
    {
        if (verification.FailingTests is { } failing)
        {
            var baseline = verification.BaselineFailingTests ?? Array.Empty<string>();
            var regressions = failing.Where(t => !baseline.Contains(t)).ToList();
            if (regressions.Count > 0)
                return KeystoneVerdict.Fail(
                    $"This change introduced {regressions.Count} NEW test failure(s): "
                    + $"{string.Join(", ", regressions)}. "
                    + "Pre-existing failures already red at HEAD are not gated.");
            return KeystoneVerdict.Ok();
        }

        // Back-compat: no failing-test lists → the original binary gate.
        if (verification.Status == VerificationStatus.Failed)
            return KeystoneVerdict.Fail(
                "The coding agent reported a FAILED verification (build or tests not "
                + $"green): {verification.Summary ?? "no detail"}.");
        if (verification.TestsRan && !verification.TestsPassed)
            return KeystoneVerdict.Fail("Tests did not pass — run recorded as failed.");
        return KeystoneVerdict.Ok();
    }
}

public sealed record KeystoneVerdict(bool Satisfied, string? FailureReason)
{
    public static KeystoneVerdict Ok() => new(true, null);
    public static KeystoneVerdict Fail(string reason) => new(false, reason);
}
