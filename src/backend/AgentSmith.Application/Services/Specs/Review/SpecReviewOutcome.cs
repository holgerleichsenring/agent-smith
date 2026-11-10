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

    /// <summary>2026-11-10-8e59: the final text is not the one the last taken round attacked.</summary>
    public bool Unattacked { get; init; }

    /// <summary>True when the spec may be built in this attempt. 2026-11-10-8e59: only when a round said build on
    /// its current text; anything else is built only after the author answers the review objection.</summary>
    public bool IsCleared => StandingVerdict is null && BudgetStop is null && Failure is null;
}
