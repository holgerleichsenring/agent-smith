using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Two-layer resolver: per-project SandboxConfig fields win field-by-field
/// over the top-level <see cref="SandboxGlobalConfig"/>. Missing version is
/// a configuration error — caught here with a clear message rather than
/// surfacing as an ErrImagePull on the spawned pod.
/// </summary>
public sealed class AgentImageResolver(IOptions<SandboxGlobalConfig> globalConfig) : IAgentImageResolver
{
    public string Resolve(ResolvedProject projectConfig)
    {
        var global = globalConfig.Value;
        var registry = FirstNonEmpty(projectConfig.Sandbox?.AgentRegistry, global.AgentRegistry);
        var version = FirstNonEmpty(projectConfig.Sandbox?.AgentVersion, global.AgentVersion);

        if (string.IsNullOrEmpty(version))
        {
            throw new InvalidOperationException(
                "Sandbox agent version is not configured. Set 'sandbox.agent_version' at the top level of agentsmith.yml or under projects.<name>.sandbox.agent_version. " +
                "Operator should pin a published tag (e.g. '0.48.0') matching the agent-smith release in use.");
        }

        return string.IsNullOrEmpty(registry)
            ? $"{AgentImageDefaults.SandboxAgentImageName}:{version}"
            : $"{registry}/{AgentImageDefaults.SandboxAgentImageName}:{version}";
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrEmpty(a) ? a : b;
}
