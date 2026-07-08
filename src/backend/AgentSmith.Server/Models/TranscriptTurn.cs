namespace AgentSmith.Server.Models;

/// <summary>
/// One ordered turn in a spec-dialog transcript.
/// </summary>
public sealed record TranscriptTurn(TranscriptRole Role, string Text, DateTimeOffset At);
