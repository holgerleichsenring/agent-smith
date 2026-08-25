namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-08-25-61f1: one candidate row of a repair, reduced to the three things the repair
/// needs to know about it — which row it is, what makes it the same fact as another, and
/// when it was written. The last one settles which copy survives: duplicates of one event
/// share their payload but NOT their insert stamps, because each replay stamped its own,
/// so the earliest is the one that reconstructs the run as it happened.
/// </summary>
public readonly record struct RepairRow(long Id, string Key, DateTimeOffset CreatedAt);
