namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: a series' progress — its tickets, the landed ones, the ones whose pull request waits (open,
/// or opened and not read yet), and the ones never started (no attempt, no pull request). 2026-11-23-31dcc: and the held
/// ones — not landed, nothing waiting to merge, the newest attempt stopped short (asking, failed, cancelled, shortfall).
/// The rest are at work, so the four need not add up to the total.</summary>
public sealed record SeriesRunCounts(int Total, int Landed, int Waiting, int Held, int NotStarted);
