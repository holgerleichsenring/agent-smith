namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0167a: pr-review pipeline keys. The PR-event webhook seeds the identifying
/// keys (PrNumber, PrAuthor, plus CheckoutBranch = PR head branch) into the
/// initial context; AnalyzePrDiffHandler publishes the structured diff and the
/// authoritative head/base shas from the source platform.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>The PR / MR identifier on its platform (string — GitHub/GitLab
    /// number, AzDO pullRequestId). Seeded by the pr-event webhook handlers;
    /// read by AnalyzePrDiffHandler to fetch the diff.</summary>
    public const string PrNumber = "PrNumber";

    /// <summary>Structured diff of the PR under review
    /// (<c>AgentSmith.Domain.Models.PrDiffAnalysis</c>: per-file hunks with
    /// old/new line numbers, binary files as metadata-only entries). Published
    /// by AnalyzePrDiffHandler; consumed by the pr-review skill rounds (p0167b)
    /// and CompilePrReviewFindings (p0167c).</summary>
    public const string PrDiff = "PrDiff";

    /// <summary>Head commit sha of the PR under review. Provisionally seeded by
    /// the webhook payload, overwritten with the source platform's value by
    /// AnalyzePrDiffHandler.</summary>
    public const string PrHead = "PrHead";

    /// <summary>Base commit sha the PR diffs against. Provisionally seeded by
    /// the webhook payload where available, overwritten by AnalyzePrDiffHandler.</summary>
    public const string PrBase = "PrBase";

    /// <summary>Login / username of the PR author. Seeded by the pr-event
    /// webhook handlers.</summary>
    public const string PrAuthor = "PrAuthor";

    /// <summary>Compiled review output
    /// (<c>AgentSmith.Domain.Models.PrReviewSummary</c>: top-level summary
    /// comment + capped inline comments). Published by
    /// CompilePrReviewFindingsHandler; consumed by PostPrCommentsHandler
    /// (p0167c).</summary>
    public const string PrReviewSummary = "PrReviewSummary";
}
