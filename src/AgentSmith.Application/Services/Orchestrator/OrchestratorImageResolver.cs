using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Orchestrator;

/// <summary>
/// Two-layer resolver: per-project OrchestratorConfig fields win field-by-field
/// over the top-level <see cref="OrchestratorGlobalConfig"/>. Missing version is
/// a configuration error — caught here with a clear message rather than
/// surfacing as an ErrImagePull on the spawned orchestrator.
/// </summary>
public sealed class OrchestratorImageResolver(IOptions<OrchestratorGlobalConfig> globalConfig) : IOrchestratorImageResolver
{
    public string Resolve(ProjectConfig projectConfig)
    {
        var global = globalConfig.Value;
        var registry = FirstNonEmpty(projectConfig.Orchestrator?.Registry, global.Registry);
        var version = FirstNonEmpty(projectConfig.Orchestrator?.Version, global.Version);

        if (string.IsNullOrEmpty(version))
        {
            throw new InvalidOperationException(
                "Orchestrator image version is not configured. Set 'orchestrator.version' at the top level of agentsmith.yml or under projects.<name>.orchestrator.version. " +
                "Operator should pin a published tag (e.g. '0.49.0') matching the agent-smith release in use.");
        }

        return string.IsNullOrEmpty(registry)
            ? $"{AgentImageDefaults.OrchestratorImageName}:{version}"
            : $"{registry}/{AgentImageDefaults.OrchestratorImageName}:{version}";
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrEmpty(a) ? a : b;
}
