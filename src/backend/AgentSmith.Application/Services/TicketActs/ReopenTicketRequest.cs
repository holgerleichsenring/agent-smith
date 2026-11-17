namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061a: the ticket a 'reopen' run clears the close mark of, by its id and its key.</summary>
public sealed record ReopenTicketRequest(string TicketId, string Key);
