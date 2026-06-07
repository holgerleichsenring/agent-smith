namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// The single-run lease. A UNIQUE(Project, TicketId) index makes a duplicate
/// claim a database error (the guard — NOT the heartbeat). Claim = INSERT,
/// normal end = DELETE. HeartbeatAt is liveness only: the reaper (p0246b) uses a
/// stale heartbeat + positive evidence to release a crashed lease.
/// </summary>
public sealed class ActiveRun : EntityBase
{
    public long Id { get; set; }
    public string Project { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset ClaimedAt { get; set; }
    public DateTimeOffset HeartbeatAt { get; set; }

    public Run? Run { get; set; }
}
