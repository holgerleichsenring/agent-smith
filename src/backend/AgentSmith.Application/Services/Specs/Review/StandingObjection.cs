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
    /// <summary>2026-11-10-8e59: the hash of the text the objection is about (a spec's yaml or the contract), so an
    /// answer binds to that text and a re-cut that still draws an objection is asked again; null on an older record.</summary>
    public string? TextHash { get; init; }

    public string MarkerKey => (SpecId ?? "series:" + SeriesPrefixHead) + (TextHash is { Length: >= 12 } h ? $" at text {h[..12]}" : string.Empty);

    public const string UnattackedText =
        "the text below came from the review's last changes, and no reviewer attacked it in this form";

    /// <summary>The objection a spec's review ended on, or null: any review not cleared after the loop spent its
    /// rounds — a REFUSE, a cut that still stands, or a final text no round attacked (2026-11-10-8e59) — never a
    /// loop the budget cut short or a revision that failed.</summary>
    public static StandingObjection? Of(SpecReviewOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome is not { StandingVerdict: not null, BudgetStop: null, Failure: null }) return null;
        IReadOnlyList<SpecReviewFinding> findings = outcome.Unattacked
            ? [.. outcome.StandingFindings, new SpecReviewFinding(UnattackedText, $"round {outcome.Rounds}")]
            : outcome.StandingFindings;
        return new StandingObjection(outcome.Final.SpecId, findings)
        {
            TextHash = Contracts.Specs.ReviewedText.Hash(outcome.Final.Yaml),
        };
    }
}
