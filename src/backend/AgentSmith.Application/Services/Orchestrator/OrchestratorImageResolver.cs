using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Orchestrator;

/// <summary>
/// Resolves the orchestrator image from the deployment-wide pin in
/// <see cref="OrchestratorGlobalConfig"/> (fed by the top-level <c>deployment:</c> /
/// <c>orchestrator:</c> block). p0281c removed the per-project image override — the
/// orchestrator image is a deployment concern, not a project one; a per-project
/// orchestrator.registry/version is accepted-but-ignored and warned. Missing version is a
/// configuration error caught here rather than surfacing as an ErrImagePull on the pod.
/// </summary>
public sealed class OrchestratorImageResolver(
    IOptions<OrchestratorGlobalConfig> globalConfig,
    ILogger<OrchestratorImageResolver>? logger = null) : IOrchestratorImageResolver
{
    private readonly ILogger _logger = logger ?? NullLogger<OrchestratorImageResolver>.Instance;

    public string Resolve(ResolvedProject projectConfig)
    {
        WarnIfProjectOverride(projectConfig);
        var global = globalConfig.Value;

        if (string.IsNullOrEmpty(global.Version))
        {
            throw new InvalidOperationException(
                "Orchestrator image version is not configured. Set 'deployment.version' (or the legacy 'orchestrator.version') at the top level of agentsmith.yml. " +
                "Operator should pin a published tag (e.g. '0.49.0') matching the agent-smith release in use.");
        }

        return string.IsNullOrEmpty(global.Registry)
            ? $"{AgentImageDefaults.OrchestratorImageName}:{global.Version}"
            : $"{global.Registry}/{AgentImageDefaults.OrchestratorImageName}:{global.Version}";
    }

    private void WarnIfProjectOverride(ResolvedProject project)
    {
        if (string.IsNullOrEmpty(project.Orchestrator?.Registry) &&
            string.IsNullOrEmpty(project.Orchestrator?.Version)) return;
        _logger.LogWarning(
            "Project '{Project}': per-project orchestrator.registry/version is ignored (p0281c) — the " +
            "orchestrator image is a deployment-wide pin (deployment.version / top-level orchestrator). " +
            "Remove the per-project override; orchestrator.resources still applies.",
            project.Name);
    }
}
