namespace AgentSmith.Server.Models;

/// <summary>2026-10-15-114fa: the ticket whose close mark a Reopen cleared.</summary>
public sealed record TicketReopenAnswer(string Project, string TicketId, string TicketKey);
