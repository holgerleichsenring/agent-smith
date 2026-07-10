namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0320c: one capacity-queue entry per (Project, TicketId) waiting for sandbox
/// room. The identity Id IS the FIFO order — strict arrival order, no overtaking.
/// ReservedRunId points at the single visible "queued" Run row that becomes the
/// running row on launch. A null InitialContextJson marks a TOCTOU-backstop entry
/// (projected from RunFinished status="queued"): the poller funnel re-launches
/// those with a fresh envelope, the pump skips them.
/// </summary>
public sealed class QueuedTicket : EntityBase
{
    public long Id { get; set; }
    public string Project { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string Pipeline { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? ReservedRunId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset EnqueuedAt { get; set; }
    public string? InitialContextJson { get; set; }
    public string? PlanAnswersJson { get; set; }
}
