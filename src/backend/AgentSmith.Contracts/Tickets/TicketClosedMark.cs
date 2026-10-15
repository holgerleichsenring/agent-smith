namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-10-15-114fa: a person closed a ticket — keyed by the tracker connection and the ticket key, overwritten
/// by a later close and deleted by a Reopen. It is not a landing: a ticket that requires it keeps waiting.
/// </summary>
/// <param name="Tracker">The tracker CONNECTION's catalog name.</param>
/// <param name="TicketKey">The ticket's key, <c>TicketKey.For(platform, id)</c>.</param>
/// <param name="TicketId">The ticket's id in the tracker's own spelling.</param>
/// <param name="Project">The project the ticket was closed in.</param>
/// <param name="ClosedAt">When the person closed it.</param>
/// <param name="By">The person who closed it.</param>
/// <param name="Note">Why, trimmed; null when no note was given.</param>
/// <param name="ClosedPullRequests">The pull-request URLs this close closed on their hosts.</param>
/// <param name="TrackerClosed">Whether this close closed the ticket on its tracker.</param>
public sealed record TicketClosedMark(
    string Tracker,
    string TicketKey,
    string TicketId,
    string Project,
    DateTimeOffset ClosedAt,
    string By,
    string? Note,
    IReadOnlyList<string> ClosedPullRequests,
    bool TrackerClosed);
