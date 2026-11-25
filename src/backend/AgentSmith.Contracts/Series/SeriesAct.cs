namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-25-9992e: one act of the series master, logged on its record — when, which tool, on which ticket (its id; "final"
/// for the series' final pull requests, null for a series-wide act), what it did in words ("merged SCRUM-12 (now)"; the tool's
/// name when the act said nothing), and the one sentence of why the master gave. What the act answered goes to the log only,
/// so the record keeps a short line per act.
/// </summary>
public sealed record SeriesAct(DateTimeOffset At, string Tool, string? Ticket, string Text, string? Reason);
