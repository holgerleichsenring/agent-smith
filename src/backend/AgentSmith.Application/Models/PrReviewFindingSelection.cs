using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Result of the pr-review render-budget selection: Inline holds at most the
/// budget of anchor groups (most severe first); SummaryOnly holds every
/// finding that folds into the top-level comment instead — anchors beyond the
/// budget, findings without a line anchor, and anchors that don't hit the PR
/// diff's new-side lines.
/// </summary>
public sealed record PrReviewFindingSelection(
    IReadOnlyList<PrReviewFindingGroup> Inline,
    IReadOnlyList<SkillObservation> SummaryOnly,
    int TotalFindings);
