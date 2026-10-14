using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: a REFUSE still standing when a review ran out — its admitted findings with their evidence.
/// <paramref name="SpecId"/> names the spec; null is a series objection, whose
/// <paramref name="SeriesPrefixHead"/> is the first member of the prefix it reviewed (2026-10-13-8762k).
/// <see cref="MarkerKey"/> is what the ticket thread is searched for, so a series objection on a later prefix
/// is a new one.
/// </summary>
public sealed record StandingObjection(
    string? SpecId, IReadOnlyList<SpecReviewFinding> Findings, string? SeriesPrefixHead = null)
{
    public string MarkerKey => SpecId ?? "series:" + SeriesPrefixHead;

    /// <summary>The objection a spec's review ended on, or null: only a REFUSE standing after the loop spent its
    /// rounds or ended on a second REFUSE — never a loop the budget cut short or a revision that failed.</summary>
    public static StandingObjection? Of(SpecReviewOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome is { StandingVerdict: SpecReviewVerdicts.Refuse, BudgetStop: null, Failure: null }
            ? new StandingObjection(outcome.Final.SpecId, outcome.StandingFindings)
            : null;
    }
}
