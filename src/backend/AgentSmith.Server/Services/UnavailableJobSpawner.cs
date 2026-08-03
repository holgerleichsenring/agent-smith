using AgentSmith.Contracts.Providers;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0393: the spawner that exists when no spawner backend does.
///
/// AddJobSpawnerAsync probes Docker, then Kubernetes, and on failure registered
/// NOTHING — it logged "'fix' commands will be disabled" and moved on. But
/// OrphanJobDetector is a hosted service that takes an IJobSpawner, so the container
/// could not construct it and StartAsync threw. The server did not degrade; it died,
/// on the absence of a dependency, which is precisely what p0391a's operator ruling
/// exists to prevent: a dead process reports nothing, and an operator who cannot be
/// told what is broken is as incapable as the service.
///
/// This makes the intent real. Composition succeeds, the server comes up, the missing
/// backend is a BLOCKING startup finding the operator can read at /api/config/findings,
/// and every spawn attempt fails with the same reason rather than a null reference.
/// </summary>
public sealed class UnavailableJobSpawner(string reason, ILogger<UnavailableJobSpawner> logger)
    : IJobSpawner
{
    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ConnectionProbeResult.Unreachable(0, reason));

    public Task<string> SpawnAsync(JobRequest request, CancellationToken cancellationToken)
    {
        // Loud, and at the moment of the attempt: the startup finding says the capability
        // is gone, this says which run wanted it.
        logger.LogError(
            "Cannot spawn a job for project {Project}: no spawner backend is reachable ({Reason})",
            request?.Project ?? "(unknown)", reason);
        throw new InvalidOperationException(
            $"No job-spawner backend is reachable: {reason}");
    }

    // A job that was never spawned is not alive, and terminating it is a no-op. Both
    // answers are correct rather than defensive — the reaper and the cancel enforcer
    // must keep working on a degraded server.
    public Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task TerminateAsync(string jobId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
