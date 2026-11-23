namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: a series' progress — its tickets, the landed ones, the ones whose pull request waits (open,
/// or opened and not read yet), and the ones never started (no attempt, no pull request). The rest are at work or held,
/// so the three need not add up to the total.</summary>
public sealed record SeriesRunCounts(int Total, int Landed, int Waiting, int NotStarted);
