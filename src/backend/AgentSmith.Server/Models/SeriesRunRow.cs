namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: one series run in the series list.</summary>
/// <param name="Name">The conversation's subject, else the first ticket's title.</param>
/// <param name="NextMerge">The key of the ticket whose pull requests merge next; null when none is ready.</param>
/// <param name="Question">The master's question still unanswered; null when none waits.</param>
public sealed record SeriesRunRow(
    string Id, string Name, string Project, string State, SeriesRunCounts Counts, string? NextMerge, string? Question,
    DateTimeOffset LastActivityAt);
