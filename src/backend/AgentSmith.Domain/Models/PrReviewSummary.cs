namespace AgentSmith.Domain.Models;

/// <summary>
/// The compiled output of a pr-review run — the value behind
/// ContextKeys.PrReviewSummary. TopLevelComment is the summary posted once per
/// PR (severity counts, folded excess findings, link to the run's result.md);
/// InlineComments carry at most the render budget of per-line comments
/// (grouped by file + line range, most severe first).
/// </summary>
public sealed record PrReviewSummary(
    string TopLevelComment,
    IReadOnlyList<PrReviewInlineComment> InlineComments);
