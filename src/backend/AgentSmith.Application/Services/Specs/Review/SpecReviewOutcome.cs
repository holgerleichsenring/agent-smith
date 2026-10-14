using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: how one spec's review ended. <paramref name="StandingVerdict"/> is null when the stop rule
/// held, else the verdict still standing ("not taken" when no reviewer answered). A <paramref name="BudgetStop"/>
/// is a spec cut mid-review — not cleared, so not built in this attempt; a <paramref name="Failure"/> is a
/// revision the branch did not take, which fails the step.
/// </summary>
public sealed record SpecReviewOutcome(
    SpecDraft Final,
    int Rounds,
    string? StandingVerdict,
    IReadOnlyList<SpecReviewFinding> StandingFindings,
    IReadOnlyList<SpecReviewResidue> Residues,
    string? BudgetStop = null,
    string? Failure = null)
{
    public const string NotTaken = "not taken";

    /// <summary>True when the spec may be built in this attempt.</summary>
    public bool IsCleared => BudgetStop is null && Failure is null;
}
