namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-dd05b: the ticket whose body a 'ticket-body' run renders from its branch, and who asked for it.</summary>
public sealed record TicketBodyRequest(string TicketId, string By);
