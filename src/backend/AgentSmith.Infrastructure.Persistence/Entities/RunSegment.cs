namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59j: one launch of a run — its first start, or a relaunch after a park or a requeue — from its
/// RunStarted to the RunFinished that closed it. The checkpoint keeps only the latest park and the trail is
/// pruned, so this row is what says when each launch began and ended. A park is a segment that ended waiting.
/// </summary>
public sealed class RunSegment : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>The trail position of the launch's RunStarted — the row's identity.</summary>
    public long? EventSeq { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>The trail position of the closing RunFinished; null while open, or when the terminal
    /// reconciler closed it outside the drain.</summary>
    public long? EndSeq { get; set; }

    /// <summary>Null while the launch runs.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>The closing RunFinished's status — 'waiting_for_input' a park, 'queued' a capacity wait.</summary>
    public string? EndStatus { get; set; }
}
