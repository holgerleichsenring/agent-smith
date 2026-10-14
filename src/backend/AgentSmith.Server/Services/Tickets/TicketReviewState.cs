using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59b: a pull request's record as a ticket shows it. The decision comes from the
/// standing entries only — rejected over changes requested over untrusted changes requested over
/// approved, the newest winning among equals. The last activity is derived, never stored: the
/// newest entry, else when the pull request was opened, else when its state began.
/// </summary>
public sealed class TicketReviewState
{
    private static readonly string[] Rank =
    [
        TicketReviewDecisions.Approved, TicketReviewDecisions.ChangesRequestedUntrusted,
        TicketReviewDecisions.ChangesRequested, TicketReviewDecisions.Rejected,
    ];

    /// <param name="openedAt">When each pull request (by PrKey) was first recorded opened; empty where not read.</param>
    public IReadOnlyList<TicketPullRequestView> Of(
        IEnumerable<PrReviewRecordView> records, IReadOnlyDictionary<string, DateTimeOffset> openedAt) =>
        [.. records.Select(r => Of(r, openedAt.TryGetValue(r.PrKey, out var at) ? at : null))];

    public TicketPullRequestView Of(PrReviewRecordView record, DateTimeOffset? openedAt)
    {
        var shown = record.Entries.Where(e => e.Standing && e.Decision is not null)
            .Select(e => (Entry: e, Decision: Word(e)))
            .OrderByDescending(d => Array.IndexOf(Rank, d.Decision)).ThenByDescending(d => d.Entry.At)
            .FirstOrDefault();
        var decision = shown.Decision ?? TicketReviewDecisions.None;
        return new TicketPullRequestView(record.Number, record.Url, record.Repo, record.State, decision,
            shown.Entry?.AuthorLogin, record.LastEntryAt ?? openedAt ?? record.StateAt, record.StateAt,
            decision == TicketReviewDecisions.ChangesRequestedUntrusted ? TicketReviewDecisions.UntrustedNote : null);
    }

    private static string Word(PrReviewEntryView entry) => entry.Decision switch
    {
        PrReviewDecisions.Rejected => TicketReviewDecisions.Rejected,
        PrReviewDecisions.ChangesRequested => entry.Trusted
            ? TicketReviewDecisions.ChangesRequested : TicketReviewDecisions.ChangesRequestedUntrusted,
        _ => TicketReviewDecisions.Approved,
    };
}
