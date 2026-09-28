using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using k8s;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Can the Kubernetes sandbox backend reach its API? Lists one pod in the namespace sandboxes
/// are created in — the permission the backend actually uses there, so a denied cluster-wide
/// read cannot report a working backend as broken. Read-only, never throws.
/// </summary>
public sealed class KubernetesSandboxBackendProbe(
    IKubernetes kubernetes,
    KubernetesSandboxOptions options,
    ILogger<KubernetesSandboxBackendProbe> logger) : IPreflightSandboxProbe
{
    public string BackendLabel => "Kubernetes";

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await kubernetes.CoreV1.ListNamespacedPodAsync(
                options.Namespace, limit: 1, cancellationToken: cancellationToken);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Kubernetes API probe failed for namespace {Namespace}", options.Namespace);
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }
}
