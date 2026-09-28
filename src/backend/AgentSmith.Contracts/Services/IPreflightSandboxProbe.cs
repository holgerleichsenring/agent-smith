using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0324: backend-specific probe behind the sandbox-spawn preflight check. The CLI and the
/// server's in-process backend prove a real spawn + exec round-trip through ISandboxFactory;
/// the server's Kubernetes and Docker backends prove their API or daemon answers. Never throws.
/// </summary>
public interface IPreflightSandboxProbe
{
    /// <summary>Human label for the probed backend, e.g. "in-process", "Kubernetes".</summary>
    string BackendLabel { get; }

    Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken);
}
