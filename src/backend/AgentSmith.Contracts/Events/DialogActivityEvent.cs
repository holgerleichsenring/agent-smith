namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-10-d854c: one step a design turn reported — a tool, a model call, reviewing or revising — kept on the
/// turn's run, so every step is readable after the turn ends or the server restarts. <see cref="Seq"/> is the step's
/// place in its turn, from 1; <see cref="Detail"/> is bounded to <see cref="MaxDetail"/> characters.
/// </summary>
public sealed record DialogActivityEvent(
    string RunId,
    DateTimeOffset Timestamp,
    string Kind,
    string? Name,
    string? Detail,
    int Seq,
    DateTimeOffset TurnStartedAt)
    : RunEvent(RunId, EventType.DialogActivity, Timestamp)
{
    public const int MaxDetail = 500;
}
