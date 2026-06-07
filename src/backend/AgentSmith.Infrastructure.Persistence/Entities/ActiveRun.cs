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

    // Null between claim (INSERT at enqueue time) and run start — the lease is
    // taken before the Run row exists; the lifecycle coordinator attaches the
    // run id once it begins. The FK is therefore optional.
    public string? RunId { get; set; }

    // The orchestrator's container/pod handle, set when the run is spawned. The
    // reaper's POSITIVE-EVIDENCE probe asks the orchestrator whether this handle
    // is still present before releasing the lease.
    public string? JobId { get; set; }

    public DateTimeOffset ClaimedAt { get; set; }
    public DateTimeOffset HeartbeatAt { get; set; }

    public Run? Run { get; set; }
}
