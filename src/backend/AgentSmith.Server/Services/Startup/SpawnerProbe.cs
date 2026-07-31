using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: did the job spawner compose? AddJobSpawnerAsync already swallows a failed
/// Docker ping or an absent in-cluster config and simply registers nothing, which reads
/// downstream as an unrelated "no service for type IJobSpawner" the first time a run is
/// dispatched. Absent here is a named finding at boot instead.
/// </summary>
public sealed class SpawnerProbe(IServiceProvider services) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.Spawner;

    public Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StartupFinding>>(
            services.GetService<IJobSpawner>() is not null ? [] : [Unavailable()]);

    private static StartupFinding Unavailable() => new(
        StartupSubsystems.Spawner,
        StartupFindingSeverity.Blocking,
        "No job spawner is composed, so no run can be dispatched to a sandbox. Neither the "
        + "Docker socket nor an in-cluster Kubernetes config was usable at startup — see the "
        + "startup log for the reason the backend was skipped.",
        Field: "SANDBOX_TYPE");
}
