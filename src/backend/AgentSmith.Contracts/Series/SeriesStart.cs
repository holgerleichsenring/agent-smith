namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-19-1b72a: what a series' start of one member ticket came to — whether it started, and the sentence
/// of what happened.</summary>
public sealed record SeriesStart(bool Started, string Text);
