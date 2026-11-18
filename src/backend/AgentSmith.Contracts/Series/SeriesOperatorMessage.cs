namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207b: an operator message of the conversation addressed to the series, with its transcript
/// index — the cursor the master reads past.</summary>
public sealed record SeriesOperatorMessage(int Index, string Text, DateTimeOffset At);
