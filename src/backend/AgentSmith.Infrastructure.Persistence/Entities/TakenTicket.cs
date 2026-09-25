namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-09-25-b4d9: one row per (Project, TicketId) — a ticket the framework took up and has
/// not finished. Unlike the ActiveRun lease beside it, this row is NOT deleted by the reaper:
/// the lease states liveness and is rightly transient, this row states that work is owed.
/// </summary>
public sealed class TakenTicket : EntityBase
{
    public long Id { get; set; }
    public string Project { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;

    /// <summary>The tracker platform the claim came from, as the envelope spells it.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// The pipeline the claim was granted for. The lease has no such column, which is why
    /// re-enqueueing used to re-derive it from the ticket's labels.
    /// </summary>
    public string Pipeline { get; set; } = string.Empty;

    /// <summary>Which loop owns the ticket, as <c>TakenTicketState</c>.</summary>
    public int State { get; set; }
}
