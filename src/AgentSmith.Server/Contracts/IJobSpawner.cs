using AgentSmith.Server.Models;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// Abstraction over the mechanism used to spawn ephemeral agent jobs.
/// Implementations: KubernetesJobSpawner (prod) and DockerJobSpawner (local dev).
/// The correct implementation is selected at startup via SPAWNER_TYPE env var.
/// </summary>
public interface IJobSpawner
{
    /// <summary>
    /// Spawns an ephemeral agent job for the given request (chat-intent flow:
    /// Slack/Teams "fix #123 in my-project"). Returns the jobId so progress
    /// can be tracked via Redis Streams.
    /// </summary>
    Task<string> SpawnAsync(JobRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// p0113: spawns an ephemeral agent job for a queue-claimed PipelineRequest.
    /// The container picks up its work by reading from the IPipelineRequestStore
    /// using the supplied jobId — CLI args stay short and structured request
    /// data round-trips losslessly through Redis.
    /// </summary>
    Task SpawnQueueJobAsync(
        string jobId, string redisUrl, string configPath, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the container/pod for the given job is still running.
    /// Returns false if the container has exited or cannot be found.
    /// </summary>
    Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken);
}
