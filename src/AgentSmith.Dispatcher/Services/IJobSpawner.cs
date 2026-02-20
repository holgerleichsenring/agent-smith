using AgentSmith.Dispatcher.Models;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Abstraction over the mechanism used to spawn ephemeral agent jobs.
/// Implementations: KubernetesJobSpawner (prod) and DockerJobSpawner (local dev).
/// The correct implementation is selected at startup via SPAWNER_TYPE env var.
/// </summary>
public interface IJobSpawner
{
    /// <summary>
    /// Spawns an ephemeral agent job for the given fix-ticket intent.
    /// Returns the jobId that can be used to track progress via Redis Streams.
    /// </summary>
    Task<string> SpawnAsync(FixTicketIntent intent, CancellationToken cancellationToken = default);
}
