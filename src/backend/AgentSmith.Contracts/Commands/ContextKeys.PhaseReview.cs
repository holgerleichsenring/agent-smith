namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-09-17-042eh: the keys the phase review and its one fix pass carry. Their own file,
/// because <see cref="ContextKeys"/>' spec partial is at the length limit and a key the
/// ratchet forced into a baseline row would be the first of many.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>2026-09-17-042eh: sandbox key → the commit the sandbox stood at when the
    /// CURRENT phase was selected (IReadOnlyDictionary&lt;string, string&gt;). The phase
    /// review diffs from here to the working tree, so it reads this phase's own work and
    /// not the run's. Rewritten at every SelectPhase.</summary>
    public const string PhaseStartHeads = "PhaseStartHeads";

    /// <summary>2026-09-17-042eh: the findings the phase review kept
    /// (IReadOnlyList&lt;<see cref="Domain.Models.PhaseFinding"/>&gt;) — what the fix pass is
    /// given, what the record states and what the artifact carries. Cleared at phase start.</summary>
    public const string PhaseReviewFindings = "PhaseReviewFindings";

    /// <summary>2026-09-17-042eh: set while the ONE fix pass the review earns is in flight, so
    /// the verification after it is routed to the revert instead of recorded as a failure, and
    /// so the review at the end of the splice knows it is the second one.</summary>
    public const string PhaseReviewFixPass = "PhaseReviewFixPass";

    /// <summary>2026-09-17-042eh: why the fix pass was reverted, or absent. A reverted pass
    /// leaves the findings standing and says so in the record and the pull request.</summary>
    public const string PhaseReviewReverted = "PhaseReviewReverted";

    /// <summary>2026-09-17-042eh: <see cref="PhaseAccounts"/> as the FIRST verification left
    /// it, kept while the fix pass runs. A reverted fix pass whose own account stayed behind
    /// would fail a run whose delivered state is the verified one.</summary>
    public const string PhaseAccountsSnapshot = "PhaseAccountsSnapshot";

    /// <summary>2026-09-17-042eh: <see cref="RunAccounts"/> as the first verification left it,
    /// kept while the fix pass runs, for the same reason as
    /// <see cref="PhaseAccountsSnapshot"/> — the run gate reads it.</summary>
    public const string RunAccountsSnapshot = "RunAccountsSnapshot";

    /// <summary>2026-09-17-042eh: every phase's review findings
    /// (<see cref="Domain.Models.RunPhaseReviews"/>), kept like the account ledger because the
    /// pull request is built once at the end and the per-phase keys clear at SelectPhase.</summary>
    public const string RunPhaseReviews = "RunPhaseReviews";
}
