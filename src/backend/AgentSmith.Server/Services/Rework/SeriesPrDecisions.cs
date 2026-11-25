using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-25-96aec: a person's review decision on a series ticket's pull request that the stored record did not stand on yet —
/// an approval (what a merge under review: each waits for) or a change request; agent-smith's own and a bot's are left out.
/// </summary>
public static class SeriesPrDecisions
{
    public static IEnumerable<string> New(PrReviewRecordView? stored, IReadOnlyList<PrReviewEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var before = (stored?.Entries ?? []).Where(e => Counts(e.Standing, e.IsOurs, e.IsBot, e.Decision))
            .Select(e => (e.AuthorLogin, e.Decision)).ToHashSet();
        return entries.Where(e => Counts(e.Standing, e.IsOurs, e.IsBot, e.Decision) && !before.Contains((e.AuthorLogin, e.Decision)))
            .Select(e => $"{(IsApproval(e.Decision) ? "is approved" : "has changes requested")} by {e.AuthorLogin ?? "a reviewer"}")
            .Distinct(StringComparer.Ordinal);
    }

    private static bool Counts(bool standing, bool ours, bool bot, string? decision) =>
        standing && !ours && !bot && (IsApproval(decision) || decision is PrReviewDecisions.ChangesRequested or PrReviewDecisions.Rejected);

    private static bool IsApproval(string? decision) =>
        decision is PrReviewDecisions.Approved or PrReviewDecisions.ApprovedWithSuggestions;
}
