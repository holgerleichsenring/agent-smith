namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: the code runs the list read, and whether the ceiling cut older ones off.</summary>
/// <param name="DerivationCosts">2026-10-26-af69: each ticket's summed spec-derivation cost, keyed by (project, ticket id).</param>
public sealed record TicketListRows(
    IReadOnlyList<TicketRunRow> Rows, bool Truncated, IReadOnlyDictionary<(string Project, string TicketId), decimal> DerivationCosts);
