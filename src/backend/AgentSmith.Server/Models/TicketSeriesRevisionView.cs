namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59e: one revision an attempt worked from — number and cause as written, the attempt,
/// and when that attempt published its cut (null for an attempt from before the record).
/// </summary>
public sealed record TicketSeriesRevisionView(int Number, string? Cause, string RunId, int Attempt, DateTimeOffset? At);
