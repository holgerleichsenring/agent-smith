namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59e: one spec's cell — done | carried | live | asked | failed | handed_back |
/// not_started.</summary>
public sealed record TicketSeriesCellView(string SpecId, string State);
