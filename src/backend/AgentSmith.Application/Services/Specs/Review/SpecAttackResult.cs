using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: one reviewer's answer as the framework admitted it. <paramref name="Verdict"/> is one of
/// <see cref="SpecReviewVerdicts"/> and is only as severe as what remained: a refuse needs an admitted
/// finding, a build-with-cuts an admitted cut.
/// </summary>
public sealed record SpecAttackResult(
    string Verdict, IReadOnlyList<SpecReviewCut> Cuts, IReadOnlyList<SpecReviewFinding> Findings)
{
    /// <summary>2026-11-09-9631b: this reviewer could not read what it reviews, so its verdict says
    /// nothing. It leaves the merge, and a draft with no other reviewer stands as not reviewed.</summary>
    public bool Blind { get; init; }
}
