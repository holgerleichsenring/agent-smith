namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59b: one entry of a pull request's review — ONE row per host id. A decision is the
/// same host object as a note the thread reader returns, so it is a column on that row; a Kind
/// "decision" row exists only when no note carries the id. <see cref="Standing"/> is true while the
/// latest state read lists the decision — the current decision lives only there.
/// </summary>
public sealed class PrReviewEntry : EntityBase
{
    public long Id { get; set; }
    public string PrKey { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;

    /// <summary>comment, review or decision.</summary>
    public string Kind { get; set; } = string.Empty;

    public string? Decision { get; set; }
    public bool Standing { get; set; }
    public string? AuthorLogin { get; set; }
    public bool Trusted { get; set; }
    public bool IsOurs { get; set; }
    public bool IsBot { get; set; }
    public bool IsSystem { get; set; }
    public string? File { get; set; }
    public int? Line { get; set; }
    public bool? Resolved { get; set; }
    public string? ThreadHostId { get; set; }

    /// <summary>Capped at PersistenceLimits.ReviewNoteBody; <see cref="Truncated"/> says it was cut.</summary>
    public string Body { get; set; } = string.Empty;

    public bool Truncated { get; set; }
    public long AtTicks { get; set; }
}
