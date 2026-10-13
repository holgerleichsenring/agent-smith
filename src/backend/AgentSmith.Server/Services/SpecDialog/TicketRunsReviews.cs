using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-13-9a59o: a ticket's pull request records, bounded for a tool answer. The store keeps up
/// to 4000 characters a body and the dialog loop caps no tool result, so each body is cut to
/// <see cref="BodyCap"/> and only the newest <see cref="EntryCap"/> entries over ALL pull requests
/// are kept, oldest first within each. Every record stays listed, named by its readable url.
/// </summary>
public static class TicketRunsReviews
{
    public const int BodyCap = 600;
    public const int EntryCap = 40;

    public static IReadOnlyList<TicketPullRequestReview> Of(IReadOnlyList<PrReviewRecordView> records)
    {
        var kept = records
            .SelectMany(r => r.Entries.Select(e => (r.PrKey, Entry: e)))
            .OrderByDescending(x => x.Entry.At)
            .Take(EntryCap)
            .ToLookup(x => x.PrKey, x => x.Entry, StringComparer.Ordinal);
        return [.. records.Select(r => new TicketPullRequestReview(
            r.Url, r.State, r.StateAt, [.. kept[r.PrKey].OrderBy(e => e.At).Select(Entry)]))];
    }

    private static TicketReviewEntry Entry(PrReviewEntryView e)
    {
        var cut = e.Body.Length > BodyCap;
        return new TicketReviewEntry(
            e.Kind, e.Decision, e.Standing, e.AuthorLogin, e.Trusted, e.IsOurs, e.IsBot, e.IsSystem,
            e.File, e.Line, e.Resolved, cut ? e.Body[..BodyCap] : e.Body, cut || e.Truncated, e.At);
    }
}
