namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-10-12-967ab: one stored surface that still carries rows in the names used before the spec rename — a table, or a
/// set of columns on one. Rewriting it is idempotent: a second pass finds nothing to change.
/// </summary>
public interface IStoredSpecNameSurface
{
    /// <summary>What the log calls it.</summary>
    string Name { get; }

    /// <summary>Rewrites every row still in the old names; returns how many rows changed.</summary>
    Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct);
}
