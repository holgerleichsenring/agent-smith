using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Anchors the review's answer to the criteria it was asked about.
/// <para>
/// The contract is the done-list, not whatever the model wrote back. A row for a criterion
/// nobody asked about is dropped, and a criterion the answer skipped reads as decidable —
/// the floor. Both directions matter: without the first, a model can invent a criterion and
/// hand it back to a human; without the second, a truncated answer would silently shrink
/// the list the run is judged by.
/// </para>
/// </summary>
public static class SpecReviewAlignment
{
    public static IReadOnlyList<CriterionReview> Of(
        IReadOnlyList<string> criteria, IReadOnlyList<CriterionReview> rows)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(rows);
        return [.. criteria.Select(criterion => Match(criterion, rows))];
    }

    private static CriterionReview Match(string criterion, IReadOnlyList<CriterionReview> rows)
    {
        var row = rows.FirstOrDefault(
            r => string.Equals(r.Criterion?.Trim(), criterion.Trim(), StringComparison.Ordinal));
        return row is null
            ? new CriterionReview(criterion, SpecReviewDisposition.Decidable)
            : row with { Criterion = criterion };
    }
}
