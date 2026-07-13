using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Preflight;

/// <summary>
/// p0324: server-side sandbox probe — delegates to the composed job spawner's
/// reachability/capacity probe (Docker daemon or kube API + quota). The server never
/// creates sandboxes itself, so a spawn round-trip here would probe the wrong thing;
/// its spawned orchestrator pods create the actual sandboxes.
/// </summary>
internal sealed class JobSpawnerSandboxProbe(IJobSpawner jobSpawner) : IPreflightSandboxProbe
{
    public string BackendLabel =>
        jobSpawner.GetType().Name.Replace("JobSpawner", "", StringComparison.Ordinal);

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        jobSpawner.ProbeAsync(cancellationToken);
}
