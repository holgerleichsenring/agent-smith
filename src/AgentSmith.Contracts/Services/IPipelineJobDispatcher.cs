using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Application-level abstraction over how a PipelineRequest is handed off
/// for execution. Lets PipelineQueueConsumer (Application layer) stay
/// independent of the spawn mechanism (Server layer).
///
/// Production binding: Server's JobSpawnerPipelineDispatcher generates a
/// jobId, persists the request via IPipelineRequestStore, and spawns an
/// ephemeral CLI container that loads the request by jobId.
/// </summary>
public interface IPipelineJobDispatcher
{
    /// <summary>
    /// Hand off the request for execution. Returns the assigned jobId so the
    /// caller can correlate logs / heartbeats with the spawned job.
    /// </summary>
    Task<string> DispatchAsync(PipelineRequest request, CancellationToken cancellationToken);
}
