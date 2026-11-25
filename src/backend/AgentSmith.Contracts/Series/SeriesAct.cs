namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-25-9992e: one act of the series master, logged on its record — when, which tool, on which ticket (its id; "final"
/// for the series' final pull requests, null for a series-wide act), what it did in words ("merged SCRUM-12 (now)"; the tool's
/// name when the act said nothing), and the one sentence of why the master gave. What the act answered goes to the log only,
/// so the record keeps a short line per act.
/// </summary>
public sealed record SeriesAct(DateTimeOffset At, string Tool, string? Ticket, string Text, string? Reason)
{
    /// <summary>2026-11-25-96aec: when the act was called (<see cref="At"/> is when its beat began). An event recorded on its
    /// ticket between this and <see cref="DoneAt"/> was recorded by the act itself. Null on an act logged before.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>2026-11-25-96aec: when the act returned; null on an act logged before.</summary>
    public DateTimeOffset? DoneAt { get; init; }
}
