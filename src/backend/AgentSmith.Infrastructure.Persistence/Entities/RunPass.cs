namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59f: one master pass of a spec, as it ended — why it opened and ended, the verdicts it
/// reported and what it left open. One row per end event, identified by its trail position. A pass that
/// died with its run has no row; its calls still carry its ordinal.
/// </summary>
public sealed class RunPass : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public long? EventSeq { get; set; }
    public string SpecId { get; set; } = string.Empty;

    /// <summary>Run-wide, 1-based.</summary>
    public int Ordinal { get; set; }

    /// <summary>The pass within its master step; 0 is the step's first loop.</summary>
    public int PassInStep { get; set; }

    public string OpenedBecause { get; set; } = string.Empty;
    public string EndedBecause { get; set; } = string.Empty;

    /// <summary>Why the loop re-engaged after this pass; null unless it ended re-engaged.</summary>
    public string? ReengageReason { get; set; }

    public bool VerdictEmitted { get; set; }
    public int? CriteriaMet { get; set; }
    public int? CriteriaTotal { get; set; }

    /// <summary>The pass's own per-criterion verdicts as a JSON array of MasterPassCriterion; null without a verdict.</summary>
    public string? CriteriaJson { get; set; }

    public string GateLine { get; set; } = string.Empty;
    public int ActionablePending { get; set; }
    public int UnbackedDone { get; set; }
    public DateTimeOffset EndedAt { get; set; }
}
