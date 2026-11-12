namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-11-12-a1fa: an operator message stored while a design turn held the gate, waiting for that turn's next tool
/// result or for the next turn. <paramref name="Index"/> is its place in the transcript.
/// </summary>
public sealed record QueuedOperatorMessage(int Index, string Text, DateTimeOffset At, TurnPoster Poster);
