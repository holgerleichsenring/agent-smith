using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Resolves the fully-qualified sandbox agent carrier-image reference for
/// a specific project by walking projects.&lt;name&gt;.sandbox per-project
/// overrides on top of the top-level agentsmith.yml <c>sandbox:</c> block.
/// </summary>
public interface IAgentImageResolver
{
    /// <summary>Returns "{registry}/{image-name}:{version}". Throws when no version is configured at either layer.</summary>
    string Resolve(ResolvedProject projectConfig);
}
