namespace AgentSmith.Server.Models;

/// <summary>2026-10-15-114fa: whether a close closed the ticket on its tracker; the reason is a class.</summary>
public sealed record TicketTrackerCloseOutcome(bool Closed, string? Reason);
