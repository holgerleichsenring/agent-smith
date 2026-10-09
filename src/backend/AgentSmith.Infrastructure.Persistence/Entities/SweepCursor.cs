namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-08-9e6e: how far one change source has been read — a tracker's tickets or a repository's
/// pull requests. <see cref="AtTicks"/> is UTC ticks (SQLite cannot compare a DateTimeOffset) and
/// only moves forward; <see cref="Resume"/> is where a read the budget cut picks up again.
/// </summary>
public sealed class SweepCursor : EntityBase
{
    public string Source { get; set; } = string.Empty;
    public long AtTicks { get; set; }
    public string? Resume { get; set; }
}
