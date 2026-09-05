namespace AgentSmith.Domain.Models;

/// <summary>
/// What the review made of one derived phase — one row per criterion of its done-list.
/// <para>
/// <see cref="Problem"/> is set when the review could not be taken at all (no criteria, an
/// unreadable answer). A review that failed is not a review that found nothing: the first
/// must never park a run or edit a contract, and the caller has to be able to tell them
/// apart.
/// </para>
/// </summary>
public sealed record SpecReview(
    string PhaseId,
    IReadOnlyList<CriterionReview> Criteria,
    string? Problem = null)
{
    /// <summary>The demonstrated defects, in the order the done-list states them.</summary>
    public IReadOnlyList<CriterionReview> Findings => [.. Criteria.Where(c => c.IsFinding)];

    /// <summary>Findings a typed correction closes without touching what "done" means.</summary>
    public IReadOnlyList<CriterionReview> Correctable => [.. Criteria.Where(c => c.IsCorrectable)];

    /// <summary>Findings that are not correctable and therefore belong to the author.</summary>
    public IReadOnlyList<CriterionReview> ForTheAuthor =>
        [.. Criteria.Where(c => c.IsFinding && !c.IsCorrectable)];

    /// <summary>Nothing to act on — either the review found no defect, or it could not be taken.</summary>
    public bool IsQuiet => Problem is not null || Findings.Count == 0;
}
