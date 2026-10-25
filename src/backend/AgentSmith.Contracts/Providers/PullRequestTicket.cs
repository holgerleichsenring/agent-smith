using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-10-25-9dbba: the ticket a pull request references — its id, the tracker it lives in
/// and where a person opens it. A code host writes its own reference syntax only when the
/// ticket lives on that host; otherwise the web url is the reference. Null tracker means the
/// ticket's source is not a tracker this build knows.
/// </summary>
public sealed record PullRequestTicket(TicketId Id, TrackerType? Tracker, string? WebUrl)
{
    /// <summary>The reference for a fetched ticket, or null when there is none.</summary>
    public static PullRequestTicket? From(Ticket? ticket) =>
        ticket is null
            ? null
            : new PullRequestTicket(ticket.Id, ParseTracker(ticket.Source), ticket.WebUrl);

    /// <summary>The reference for a run that holds only the id and its tracker connection.</summary>
    public static PullRequestTicket? Of(TicketId? id, TrackerType tracker) =>
        id is null ? null : new PullRequestTicket(id, tracker, WebUrl: null);

    private static TrackerType? ParseTracker(string source) =>
        Enum.TryParse<TrackerType>(source, ignoreCase: true, out var tracker) ? tracker : null;
}
