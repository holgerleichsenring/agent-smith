namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061a: the close mark a person's close stores — the ticket's key, who closed it and why.</summary>
public sealed record TicketCloseMarking(string Key, string By, string? Note);
