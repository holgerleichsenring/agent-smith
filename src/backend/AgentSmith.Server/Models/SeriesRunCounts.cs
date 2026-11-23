namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: a series' progress — its tickets, the landed ones, the ones whose pull request waits (open,
/// or opened and not read yet), and the ones never started (no attempt, no pull request). 2026-11-23-31dcg: and the held
/// ones — a hold names someone (admin, decide, plan, or a merge other than the one that comes next).
/// The rest are at work, so the four need not add up to the total.</summary>
public sealed record SeriesRunCounts(int Total, int Landed, int Waiting, int Held, int NotStarted);
