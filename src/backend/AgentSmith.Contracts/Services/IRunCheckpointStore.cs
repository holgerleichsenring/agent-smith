using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0327: durable store for run checkpoints (Run.Status=waiting_for_input).
/// Relational, following the SpecDialogSession precedent — Redis stays a
/// message channel, never the authority over a parked run's state.
/// </summary>
public interface IRunCheckpointStore
{
    /// <summary>Upserts the checkpoint for its RunId (a re-checkpoint of the
    /// same run replaces the previous cursor).</summary>
    Task SaveAsync(RunCheckpointRecord checkpoint, CancellationToken cancellationToken);

    Task<RunCheckpointRecord?> GetByRunIdAsync(string runId, CancellationToken cancellationToken);

    /// <summary>All checkpoints not yet resumed (ResumedAt null), oldest first.</summary>
    Task<IReadOnlyList<RunCheckpointRecord>> ListPendingAsync(CancellationToken cancellationToken);

    /// <summary>Marks the checkpoint consumed. Returns false when it was already
    /// marked (another writer won) or does not exist.</summary>
    Task<bool> TryMarkResumedAsync(string runId, DateTimeOffset resumedAt, CancellationToken cancellationToken);
}
