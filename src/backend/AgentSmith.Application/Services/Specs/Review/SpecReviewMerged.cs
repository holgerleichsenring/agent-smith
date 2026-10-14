using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: one round's reviewers as one answer (<see cref="SpecReviewMerge"/>). <paramref name="Verdict"/>
/// is the most severe one taken, null when no reviewer was taken.
/// </summary>
public sealed record SpecReviewMerged(
    string? Verdict, IReadOnlyList<SpecReviewCut> Cuts, IReadOnlyList<SpecReviewFinding> Findings)
{
    public bool IsTaken => Verdict is not null;
}
