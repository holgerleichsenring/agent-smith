namespace AgentSmith.Contracts.Turns;

/// <summary>2026-11-12-a1fa: one message the operator sent while a turn ran, and when it was stored.
/// 2026-11-18-c8e2a: or one a master's sub-agent team delivered — a lead's word to its child, a child's finished
/// answer to its lead — under the heading it is handed on with; null is the operator's.</summary>
public sealed record MidTurnMessage(string Text, DateTimeOffset At, string? Heading = null);
