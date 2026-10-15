using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-15-114fb: a ticket's stored close mark as its row shows it, whether or not it is in force.</summary>
/// <param name="At">When the person closed it.</param>
/// <param name="By">Who closed it.</param>
/// <param name="Note">Why; null when no note was given.</param>
/// <param name="ClosedPullRequests">The pull-request URLs the close closed on their hosts.</param>
/// <param name="TrackerClosed">Whether the close closed the ticket on its tracker.</param>
public sealed record TicketClosedView(
    DateTimeOffset At, string By, string? Note, IReadOnlyList<string> ClosedPullRequests, bool TrackerClosed)
{
    /// <summary>The mark as a row shows it; null for no mark.</summary>
    public static TicketClosedView? Of(TicketClosedMark? mark) =>
        mark is null ? null : new(mark.ClosedAt, mark.By, mark.Note, mark.ClosedPullRequests, mark.TrackerClosed);
}
