using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281c: applies the single top-level <c>deployment:</c> pin (registry + version) as the
/// base for both image resolvers. Fills the legacy sandbox-agent and orchestrator image
/// fields only where the operator left them unset, so the per-image blocks still win when
/// present (back-compat) and a deployment-only config drives both from one knob. Runs on
/// the raw config before catalog resolution, so the image resolvers read the filled globals
/// unchanged.
/// </summary>
public sealed class DeploymentDefaultsApplier
{
    public void Apply(RawAgentSmithConfig raw)
    {
        var deployment = raw.Deployment;
        if (string.IsNullOrEmpty(deployment.Registry) && string.IsNullOrEmpty(deployment.Version)) return;

        ApplyOrchestrator(raw.Orchestrator, deployment);
        ApplySandbox(raw.Sandbox, deployment);
    }

    private static void ApplyOrchestrator(OrchestratorGlobalConfig orchestrator, DeploymentConfig deployment)
    {
        if (string.IsNullOrEmpty(orchestrator.Registry)) orchestrator.Registry = deployment.Registry;
        if (string.IsNullOrEmpty(orchestrator.Version)) orchestrator.Version = deployment.Version;
    }

    private static void ApplySandbox(SandboxGlobalConfig sandbox, DeploymentConfig deployment)
    {
        if (IsUnsetRegistry(sandbox.AgentRegistry) && !string.IsNullOrEmpty(deployment.Registry))
            sandbox.AgentRegistry = deployment.Registry;
        if (string.IsNullOrEmpty(sandbox.AgentVersion)) sandbox.AgentVersion = deployment.Version;
    }

    // The sandbox agent registry carries a non-empty built-in default, so "unset" means
    // empty OR still equal to that default — deployment.registry then takes authority.
    private static bool IsUnsetRegistry(string registry) =>
        string.IsNullOrEmpty(registry) || registry == AgentImageDefaults.DefaultRegistry;
}
