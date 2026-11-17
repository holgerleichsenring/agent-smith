namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-dd05b: whether a 'ticket-body' run rewrote the body, and the sentence that says how it ended.</summary>
public sealed record TicketBodyOutcome(bool Rewritten, string Message);
