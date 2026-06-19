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
        MasterVerification? verification)
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

        return KeystoneVerdict.Ok();
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
