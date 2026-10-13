namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59f: the ledger transitions of one accepted update_progress, as the trail carried them,
/// with the spec and run-wide master pass they were made in. The trail row is pruned and names neither;
/// this is the row a reader can query.
/// </summary>
public sealed class RunLedgerTransition : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public long? EventSeq { get; set; }

    /// <summary>Null when the update was made outside any spec.</summary>
    public string? SpecId { get; set; }

    /// <summary>The run-wide master pass; null outside a recorded pass.</summary>
    public int? MasterPass { get; set; }

    /// <summary>A JSON array of LedgerTransitionView, exactly as the event carried it.</summary>
    public string TransitionsJson { get; set; } = "[]";

    public DateTimeOffset At { get; set; }
}
