using System;
using System.Collections.Generic;
using System.Linq;
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
        IReadOnlyList<string> ratifiedCriteria)
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
        return EvaluateAcceptance(ratifiedCriteria, verification);
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
        IReadOnlyList<string> criteria, MasterVerification? verification)
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

        if (unresolved.Count == 0) return KeystoneVerdict.Ok();
        return KeystoneVerdict.Fail(
            $"{unresolved.Count} of {criteria.Count} acceptance criteria are unmet or unjustified: "
            + string.Join("; ", unresolved)
            + ". The run did not deliver its acceptance contract — recorded as FAILED.");
    }

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
