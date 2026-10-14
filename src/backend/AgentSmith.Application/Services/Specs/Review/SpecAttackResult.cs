using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: one reviewer's answer as the framework admitted it. <paramref name="Verdict"/> is one of
/// <see cref="SpecReviewVerdicts"/> and is only as severe as what remained: a refuse needs an admitted
/// finding, a build-with-cuts an admitted cut.
/// </summary>
public sealed record SpecAttackResult(
    string Verdict, IReadOnlyList<SpecReviewCut> Cuts, IReadOnlyList<SpecReviewFinding> Findings);
