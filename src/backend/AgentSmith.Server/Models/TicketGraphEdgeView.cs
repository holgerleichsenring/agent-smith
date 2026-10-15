namespace AgentSmith.Server.Models;

/// <summary>2026-10-14-dbcen: <paramref name="From"/> (the dependent's node id) requires <paramref name="To"/>.</summary>
public sealed record TicketGraphEdgeView(string From, string To);
