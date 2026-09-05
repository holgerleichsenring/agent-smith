using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// What a pass of the spec review left behind: the set as it now stands, what was corrected
/// in it, and what belongs to the author.
/// <para>
/// The two are kept apart because they route differently. A correction is published and the
/// run continues; a finding for the author parks the run. A pass that produced both parks —
/// a contract with one unanswerable criterion is not made whole by the others being fixed.
/// </para>
/// </summary>
public sealed record SpecReviewOutcome(
    SpecSet Set,
    IReadOnlyList<CriterionReview> Corrected,
    IReadOnlyList<CriterionReview> ForTheAuthor,
    IReadOnlyList<SpecReview> Reviews)
{
    public bool ParksTheRun => ForTheAuthor.Count > 0;

    public bool ChangedTheContract => Corrected.Count > 0;

    /// <summary>The phase the author's findings came from — the one a hand-back names.</summary>
    public string? ParkedPhaseId =>
        Reviews.FirstOrDefault(r => r.ForTheAuthor.Count > 0)?.PhaseId;
}
