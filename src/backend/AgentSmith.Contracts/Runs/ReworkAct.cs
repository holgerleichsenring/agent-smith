namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-08-e8b9b: a person's deliberate request to rework a finished ticket — who made it and
/// when, by the HOST's clock (the comment's or review's own time, never the moment a webhook was
/// received, so a redelivery is recognised as the act it already was).
/// </summary>
public sealed record ReworkAct(string Author, DateTimeOffset At);
