using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// p0324: the sandbox backend answers — via the composition's probe (CLI: a real
/// spawn + exec round-trip through ISandboxFactory; server: the job spawner's
/// reachability/capacity probe). An unreachable runtime otherwise surfaces mid-run
/// as a claimed ticket whose sandbox never comes up.
/// </summary>
public sealed class SandboxSpawnCheck(IPreflightSandboxProbe sandboxProbe) : IPreflightCheck
{
    public string Name => "sandbox-spawn";

    public string Category => "sandbox";

    public async Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var probe = await sandboxProbe.ProbeAsync(cancellationToken);
        if (probe.Ok)
            return PreflightCheckResult.Pass(
                $"{sandboxProbe.BackendLabel} backend: ok {probe.LatencyMs}ms");

        return PreflightCheckResult.Fail(
            $"{sandboxProbe.BackendLabel} backend: {probe.Error}",
            "Check the container runtime is reachable (Docker daemon / kube API), registry credentials "
            + "(registries config) allow pulling the sandbox images, and cluster quota admits a pod — "
            + "a spawn failure here otherwise shows up as a claimed ticket that never starts.");
    }
}
