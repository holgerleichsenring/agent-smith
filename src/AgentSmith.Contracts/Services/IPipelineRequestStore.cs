using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0113: cross-process handoff for queue-spawned jobs. Server saves the
/// PipelineRequest under a jobId; the spawned CLI container loads it and
/// runs the structured pipeline. CLI args stay short (jobId + redisUrl +
/// configPath) — full request payload travels via Redis with a TTL guard.
/// </summary>
public interface IPipelineRequestStore
{
    /// <summary>Save the request under the given jobId. Existing key is overwritten.</summary>
    Task SaveAsync(string jobId, PipelineRequest request, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>Returns null when the key is missing or expired.</summary>
    Task<PipelineRequest?> LoadAsync(string jobId, CancellationToken cancellationToken);
}
