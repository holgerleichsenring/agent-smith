namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0390: one row per (Project, work-spec key) — the pointer at a spec that lives
/// in git on the ticket branch. It is deliberately NOT a copy of the spec: the
/// content's readers are humans with git, and duplicating it here would create a
/// second source of truth that drifts the moment a reviewer edits the branch.
/// </summary>
public sealed class TicketWorkSpec : EntityBase
{
    public long Id { get; set; }
    public string Project { get; set; } = string.Empty;

    /// <summary>The work-spec key: &lt;provider&gt;-&lt;ticketId&gt;.</summary>
    public string SpecKey { get; set; } = string.Empty;

    /// <summary>Which repo of the resolved scope carries the spec.</summary>
    public string CarryingRepo { get; set; } = string.Empty;

    /// <summary>Sha of the last revision THIS system committed.</summary>
    public string RevisionSha { get; set; } = string.Empty;

    public int RevisionNumber { get; set; }

    /// <summary>Last hand-back case code, as <c>WorkSpecHandbackCase</c>.</summary>
    public int LastHandbackCase { get; set; }

    /// <summary>How many times in a row the same case came back with no source commit between.</summary>
    public int RepeatedHandbackCount { get; set; }

    /// <summary>Branch HEAD at the last hand-back — the "was there a source commit since" probe.</summary>
    public string? HandbackSourceSha { get; set; }
}
