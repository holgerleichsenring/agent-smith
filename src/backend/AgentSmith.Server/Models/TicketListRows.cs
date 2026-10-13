namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: the code runs the list read, and whether the ceiling cut older ones off.</summary>
public sealed record TicketListRows(IReadOnlyList<TicketRunRow> Rows, bool Truncated);
