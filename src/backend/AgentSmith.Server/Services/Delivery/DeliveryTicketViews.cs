using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-25-0099b: the span's tickets as the page lists them, newest activity first. The bucket is the ticket
/// list's, so a row says what the Tickets page it opens says; what was done is the newest attempt's specs and the
/// repositories it changed.
/// </summary>
public sealed class DeliveryTicketViews
{
    public IReadOnlyList<DeliveryTicketView> Of(
        IReadOnlyList<DeliveryTicket> tickets, IReadOnlyDictionary<(string, string), string> buckets, DateTimeOffset now) =>
        [.. tickets.Select(t => View(t, buckets.GetValueOrDefault((t.Project, t.TicketId)), now))
            .OrderByDescending(v => v.LastActivityAt)];

    private static DeliveryTicketView View(DeliveryTicket ticket, string? bucket, DateTimeOffset now)
    {
        var newest = ticket.Newest;
        var last = ticket.Attempts.Max(a => a.LastAt(now));
        if (ticket.MergedAt is { } merged && merged > last) last = merged;
        var wallEnd = ticket.MergedAt ?? last;
        return new DeliveryTicketView(
            ticket.Project, ticket.TicketId, ticket.Attempts.Select(a => a.Row.TicketTitle).LastOrDefault(t => t is not null),
            bucket, ticket.MergedAt, [.. ticket.Attempts.Select(Attempt)], newest.SpecsDone, newest.SpecsTotal,
            newest.SpecTitles, newest.Repos, ticket.Attempts.Sum(a => a.Seconds),
            Math.Max(0, (wallEnd - ticket.First.Row.StartedAt).TotalSeconds), ticket.Attempts.Sum(a => a.Row.CostUsd), last);
    }

    private static DeliveryAttemptView Attempt(DeliveryAttempt a) => new(
        a.Row.RunId, a.Row.StartedAt, a.Row.FinishedAt, a.Seconds, a.Outcome, a.Cause, a.Rework, a.Row.CostUsd,
        a.SpecsDone, a.SpecsTotal);
}
