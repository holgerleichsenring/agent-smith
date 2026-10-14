namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59b: the ticket that became Done most recently, and when.</summary>
public sealed record TicketLastDone(string Project, string TicketId, DateTimeOffset At);
