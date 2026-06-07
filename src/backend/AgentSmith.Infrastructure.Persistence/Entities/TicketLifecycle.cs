namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// The authoritative lifecycle status for a ticket. The DB is the system-of-
/// record; the platform label (p0246d) is a best-effort projection written
/// after this row and the DB wins on drift. UNIQUE(Project, Platform, TicketId).
/// </summary>
public sealed class TicketLifecycle : EntityBase
{
    public long Id { get; set; }
    public string Project { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    // UpdatedAt (the ER model's updated_at) is inherited from EntityBase — the
    // DbContext stamps it on every save.
}
