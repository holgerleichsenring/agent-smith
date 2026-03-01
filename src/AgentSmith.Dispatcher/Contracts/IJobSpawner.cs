using AgentSmith.Dispatcher.Models;

namespace AgentSmith.Dispatcher.Contracts;

/// <summary>
/// Abstraction over the mechanism used to spawn ephemeral agent jobs.
/// Implementations: KubernetesJobSpawner (prod) and DockerJobSpawner (local dev).
/// The correct implementation is selected at startup via SPAWNER_TYPE env var.
/// </summary>
public interface IJobSpawner
{
    /// <summary>
    /// Spawns an ephemeral agent job for the given request.
    /// Returns the jobId that can be used to track progress via Redis Streams.
    /// </summary>
    Task<string> SpawnAsync(JobRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the container/pod for the given job is still running.
    /// Returns false if the container has exited or cannot be found.
    /// </summary>
    Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken);
}
