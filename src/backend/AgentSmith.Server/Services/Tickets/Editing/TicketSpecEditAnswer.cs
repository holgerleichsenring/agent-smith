namespace AgentSmith.Server.Services.Tickets.Editing;

/// <summary>2026-11-17-dd05c: how a dashboard spec edit ended — the HTTP status it answers with and the sentence.</summary>
public sealed record TicketSpecEditAnswer(int Status, string Message);
