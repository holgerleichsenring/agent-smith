namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-06-03c7c: one row per (Project, ticket key) — the pointer at the series that lives in
/// git on the ticket branch. It is deliberately NOT a copy of the specs: their readers are humans
/// with git, and duplicating them here would create a second source of truth that drifts the
/// moment a reviewer edits the branch. The series' base id is CACHED here; the branch is its
/// source.
/// </summary>
public sealed class TicketSeries : EntityBase
{
    public long Id { get; set; }
    public string Project { get; set; } = string.Empty;

    /// <summary>The ticket key: &lt;provider&gt;-&lt;ticketId&gt;.</summary>
    public string TicketKey { get; set; } = string.Empty;

    /// <summary>The series' base id, <c>{yyyy-MM-dd}-{xxxx}</c>; empty until a set carried one.</summary>
    public string SeriesId { get; set; } = string.Empty;

    /// <summary>Which repo of the resolved scope carries the series.</summary>
    public string CarryingRepo { get; set; } = string.Empty;

    /// <summary>Sha of the last revision THIS system committed.</summary>
    public string RevisionSha { get; set; } = string.Empty;

    public int RevisionNumber { get; set; }

    /// <summary>Last hand-back case code, as <c>SpecHandbackCase</c>.</summary>
    public int LastHandbackCase { get; set; }

    /// <summary>How many times in a row the same case came back.</summary>
    public int RepeatedHandbackCount { get; set; }

    /// <summary>How many leading specs the record step moved to done/.</summary>
    public int ExecutedThrough { get; set; }
}
