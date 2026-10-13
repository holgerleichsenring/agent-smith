namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59e: the spec gate of the cut's attempt as it is — passed, failed, or skipped for a
/// hand-back — with the cut's spec count. The gate checks that a set exists; it validates no spec.
/// </summary>
public sealed record TicketSeriesGateView(string Status, int SpecCount, string RunId);
