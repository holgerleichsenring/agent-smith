using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0327: DB-free default (CLI one-shot runs, unit compositions). Checkpoints
/// are persisted by the SERVER-side projector from the event stream; a DB-free
/// composition publishes the event and stores nothing locally.
/// </summary>
public sealed class NoOpRunCheckpointStore : IRunCheckpointStore
{
    public Task SaveAsync(RunCheckpointRecord checkpoint, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<RunCheckpointRecord?> GetByRunIdAsync(string runId, CancellationToken cancellationToken) =>
        Task.FromResult<RunCheckpointRecord?>(null);

    public Task<IReadOnlyList<RunCheckpointRecord>> ListPendingAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RunCheckpointRecord>>([]);

    public Task<bool> TryMarkResumedAsync(string runId, DateTimeOffset resumedAt, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
