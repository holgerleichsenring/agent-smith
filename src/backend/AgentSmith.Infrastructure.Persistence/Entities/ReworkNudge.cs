namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-08-0781: one pending "check this ticket" — at most one per (project, ticket); a second
/// request merges into it. <see cref="Generation"/> moves on every merge, so a worker that handled
/// an older generation frees the row instead of deleting a request it never saw. Times are UTC
/// ticks: SQLite cannot compare a DateTimeOffset.
/// </summary>
public sealed class ReworkNudge : EntityBase
{
    public string Project { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string? PrUrl { get; set; }
    public string? Channel { get; set; }
    public int Origin { get; set; }
    public long DueTicks { get; set; }
    public long Generation { get; set; }
    public string? ClaimToken { get; set; }
    public int Tries { get; set; }
}
