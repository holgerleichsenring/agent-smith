using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>How one re-engagement pass moved the run.</summary>
public enum PassProgress
{
    /// <summary>A ledger step went done, or the verdict went green — real completion.</summary>
    Completed,
    /// <summary>State moved productively — new edits/decisions and NOT stuck on an identical red error
    /// (a greenfield build ramp, a migration mid-step, a repair whose errors are changing).</summary>
    Advanced,
    /// <summary>Edits were made but the build is red with the SAME error as last pass — churning on an
    /// identical failure. Counts toward the patience budget.</summary>
    StalledOnSameError,
    /// <summary>Nothing moved at all — no completion, no new edits/decisions. An idle pass.</summary>
    Stalled,
}

/// <summary>What the open-loop driver should do after a pass.</summary>
public enum ReengageOutcome
{
    Continue,
    /// <summary>Repetition-stall — surface for a human rather than silently truncating.</summary>
    StopStalled,
    /// <summary>An honest blocked claim backed by a concrete blocker — respected.</summary>
    StopBlocked,
}

/// <summary>The driver's decision after a pass plus the carried patience streak.</summary>
public readonly record struct ReengageStep(ReengageOutcome Outcome, int Streak);

/// <summary>
/// p0365: the open loop's forward-progress policy. Progress is STATE CHANGE, not a crisp
/// completion — the prior "a bare edit is not progress" rule (p0341c) killed a productive
/// pass on a large step (Wolverine run 5d32) and would abort a greenfield build in pass one.
/// A productive pass (new edits/decisions, or a moving red build) always continues — that
/// keeps a long greenfield ramp alive. The patience budget bounds only the CHURN case:
/// editing while the build stays red on the IDENTICAL error. A "can't" claim is honoured
/// only when a concrete blocker backs it — symmetry with the diff-verified "done" gate.
/// Pure + testable; budget + the caller's hard safety cap remain the outer bounds.
/// </summary>
public static class ReengageProgressPolicy
{
    /// <summary>Consecutive same-error churn passes tolerated before a stall is surfaced.</summary>
    public const int DefaultPatience = 3;

    /// <summary>Classify one pass from before/after ledger, verdict, change, and decision counts.</summary>
    public static PassProgress Classify(
        int doneBefore, int doneAfter,
        MasterVerification? verificationBefore, MasterVerification? verificationAfter,
        int changesBefore, int changesAfter,
        int decisionsBefore, int decisionsAfter)
    {
        if (CompletedAStep(doneBefore, doneAfter, verificationBefore, verificationAfter))
            return PassProgress.Completed;
        if (changesAfter <= changesBefore && decisionsAfter <= decisionsBefore)
            return PassProgress.Stalled;
        return StuckOnSameError(verificationBefore, verificationAfter)
            ? PassProgress.StalledOnSameError
            : PassProgress.Advanced;
    }

    /// <summary>Decide the driver action after a pass. <paramref name="streak"/> counts prior same-error passes.</summary>
    public static ReengageStep Decide(
        PassProgress progress, MasterBlockedClaim? block, int streak, int patience, bool passEndedOnException)
    {
        if (passEndedOnException) return new(ReengageOutcome.Continue, streak);   // a crash is recovery, not zero-progress
        if (ShouldRespectBlock(block)) return new(ReengageOutcome.StopBlocked, streak);
        if (progress is PassProgress.Completed or PassProgress.Advanced) return new(ReengageOutcome.Continue, 0);
        if (progress == PassProgress.StalledOnSameError)
        {
            var next = streak + 1;
            return new(next >= patience ? ReengageOutcome.StopStalled : ReengageOutcome.Continue, next);
        }
        return new(ReengageOutcome.StopStalled, streak);   // Stalled — an idle pass, surface at once
    }

    /// <summary>A blocked claim is respected only when a concrete blocker backs it (else re-driven).</summary>
    public static bool ShouldRespectBlock(MasterBlockedClaim? claim) =>
        claim is { IsBlocked: true } && !string.IsNullOrWhiteSpace(claim.Blocker);

    private static bool CompletedAStep(
        int doneBefore, int doneAfter, MasterVerification? before, MasterVerification? after) =>
        doneAfter > doneBefore || (NowPasses(after) && !NowPasses(before));

    // Both passes red with the SAME non-empty build/test tail = churning on an identical error.
    // Read from what the verdict already carries — no numeric error-count parsing.
    private static bool StuckOnSameError(MasterVerification? before, MasterVerification? after) =>
        IsRed(before) && IsRed(after)
        && !string.IsNullOrWhiteSpace(after!.Summary)
        && before!.Summary == after.Summary;

    private static bool IsRed(MasterVerification? v) => v?.Status == VerificationStatus.Failed;

    private static bool NowPasses(MasterVerification? v) =>
        v?.Status is VerificationStatus.Green or VerificationStatus.NoTests;
}
