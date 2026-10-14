using AgentSmith.Domain.Entities;

namespace AgentSmith.Server.Services.Tickets.Text;

/// <summary>One answer of the tracker for a ticket: the ticket, or the reason it could not be read, and when it was asked.</summary>
public sealed record TicketTrackerRead(Ticket? Ticket, string? Reason, DateTimeOffset At);
