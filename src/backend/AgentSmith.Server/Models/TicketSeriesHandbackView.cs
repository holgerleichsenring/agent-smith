namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59e: a hand-back cut — its case, the attempt that published it, and when.</summary>
public sealed record TicketSeriesHandbackView(string Case, string RunId, int Attempt, DateTimeOffset At);
