using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: did the job spawner compose? p0393: it now always composes — AddJobSpawnerAsync
/// registers <see cref="UnavailableJobSpawner"/> when neither Docker nor Kubernetes is
/// reachable, because registering NOTHING made OrphanJobDetector unconstructable and took
/// the whole startup with it. That was the failure this probe was written to describe and
/// could never reach: the process died before any finding could be published.
///
/// So the question changes from "is one registered" to "is the registered one real". An
/// absent registration is still a finding — nothing should be able to remove it silently.
/// </summary>
public sealed class SpawnerProbe(IServiceProvider services) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.Spawner;

    public async Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken)
    {
        var spawner = services.GetService<IJobSpawner>();
        if (spawner is null) return [Absent()];

        var probe = await spawner.ProbeAsync(cancellationToken);
        return probe.Ok ? [] : [Unreachable(probe.Error)];
    }

    private static StartupFinding Absent() => new(
        StartupSubsystems.Spawner,
        StartupFindingSeverity.Blocking,
        "No job spawner is composed at all, so no run can be dispatched to a sandbox. "
        + "One is always supposed to be registered, even when no backend is reachable — "
        + "reaching this means the composition itself changed.",
        Field: "SANDBOX_TYPE");

    private static StartupFinding Unreachable(string? reason) => new(
        StartupSubsystems.Spawner,
        StartupFindingSeverity.Blocking,
        "The job spawner cannot reach its backend, so no run can be dispatched to a sandbox. "
        + $"Cause: {reason ?? "not stated"}",
        Field: "SANDBOX_TYPE");
}
