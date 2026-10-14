using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: what the series-contract reviewer's one call left — the admitted result (cuts naming their
/// member in <see cref="SpecReviewCut.SpecId"/>), every cut admission dropped as an evidence problem, or, when the
/// call was not taken, why.
/// </summary>
public sealed record SeriesContractReview(
    SpecAttackResult? Result, IReadOnlyList<SpecEvidenceProblem> Dropped, string? NotTaken = null)
{
    public static SeriesContractReview Failed(string why) => new(null, [], why);
}
