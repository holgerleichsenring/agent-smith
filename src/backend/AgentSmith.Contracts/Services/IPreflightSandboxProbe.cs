using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0324: backend-specific probe behind the sandbox-spawn preflight check. The CLI
/// composition proves a real spawn + exec round-trip through ISandboxFactory; the
/// server composition delegates to the job spawner's capacity/reachability probe
/// (the server never creates sandboxes itself — its spawned pods do). Never throws.
/// </summary>
public interface IPreflightSandboxProbe
{
    /// <summary>Human label for the probed backend, e.g. "in-process", "Kubernetes".</summary>
    string BackendLabel { get; }

    Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken);
}
