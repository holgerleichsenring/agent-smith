namespace AgentSmith.Contracts.Turns;

/// <summary>2026-11-12-a1fa: one message the operator sent while a turn ran, and when it was stored.</summary>
public sealed record MidTurnMessage(string Text, DateTimeOffset At);
