namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-23-933da: one act on a PoC — who did what, when, why and in which run. Written only by the run event
/// projection of the slice that adds the act; unique per (run, act) so a replay inserts nothing; kept when runs are
/// deleted.
/// </summary>
public sealed class PocAuditEntry : EntityBase
{
    public long Id { get; set; }

    public long PocId { get; set; }

    public long AtTicks { get; set; }

    public string Actor { get; set; } = string.Empty;

    /// <summary>One of PocActs.</summary>
    public string Act { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string? RunId { get; set; }

    /// <summary>2026-11-23-933dc: the finding ids an 'overruled' entry lifted (a JSON array), null for every other act.</summary>
    public string? FindingIdsJson { get; set; }
}
