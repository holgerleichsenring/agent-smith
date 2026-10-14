namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59b: the tickets that became Done this month and the attempts they took.</summary>
public sealed record TicketDoneCount(int Tickets, int Attempts);
