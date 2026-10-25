namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-09-17-042eh: the keys the spec review and its one fix pass carry. Their own file,
/// because <see cref="ContextKeys"/>' spec partial is at the length limit and a key the
/// ratchet forced into a baseline row would be the first of many.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>2026-09-17-042eh: sandbox key → the commit the sandbox stood at when the
    /// CURRENT spec was selected (IReadOnlyDictionary&lt;string, string&gt;). The spec
    /// review diffs from here to the working tree, so it reads this spec's own work and
    /// not the run's. Rewritten at every SelectSpec.</summary>
    public const string SpecStartHeads = "SpecStartHeads";

    /// <summary>2026-09-17-042eh: the findings the spec review kept
    /// (IReadOnlyList&lt;<see cref="Domain.Models.SpecFinding"/>&gt;) — what the fix pass is
    /// given, what the record states and what the artifact carries. Cleared at spec start.</summary>
    public const string SpecReviewFindings = "SpecReviewFindings";

    /// <summary>2026-09-17-042eh: set while the ONE fix pass the review earns is in flight, so
    /// the verification after it is routed to the revert instead of recorded as a failure, and
    /// so the review at the end of the splice knows it is the second one.</summary>
    public const string SpecReviewFixPass = "SpecReviewFixPass";

    /// <summary>2026-09-17-042eh: why the fix pass was reverted, or absent. A reverted pass
    /// leaves the findings standing and says so in the record and the pull request.</summary>
    public const string SpecReviewReverted = "SpecReviewReverted";

    /// <summary>2026-09-17-042eh: <see cref="SpecAccounts"/> as the FIRST verification left
    /// it, kept while the fix pass runs. A reverted fix pass whose own account stayed behind
    /// would fail a run whose delivered state is the verified one.</summary>
    public const string SpecAccountsSnapshot = "SpecAccountsSnapshot";

    /// <summary>2026-09-17-042eh: <see cref="RunAccounts"/> as the first verification left it,
    /// kept while the fix pass runs, for the same reason as
    /// <see cref="SpecAccountsSnapshot"/> — the run gate reads it.</summary>
    public const string RunAccountsSnapshot = "RunAccountsSnapshot";

    /// <summary>2026-09-17-042eh: every spec's review findings
    /// (<see cref="Domain.Models.RunSpecReviews"/>), kept like the account ledger because the
    /// pull request is built once at the end and the per-spec keys clear at SelectSpec.</summary>
    public const string RunSpecReviews = "RunSpecReviews";

    /// <summary>2026-10-13-8762g: where this attempt stops building
    /// (<see cref="Specs.SpecSequenceStop"/>) — the first spec the review did not clear. In memory
    /// only; SpecSequence splices the specs before it and defers the rest.</summary>
    public const string SpecSequenceStop = "SpecSequenceStop";

    /// <summary>2026-10-25-54beb: the run's owner of its opened template scopes, installed and
    /// disposed by the pipeline executor; the master and the diff reviewer's look borrow from it.
    /// A live handle, never checkpointed.</summary>
    public const string RunTemplateScopes = "RunTemplateScopes";
}
