using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Orchestrator;

/// <summary>
/// Resolves the fully-qualified orchestrator container image reference for a
/// specific project by walking projects.&lt;name&gt;.orchestrator per-project
/// overrides on top of the top-level agentsmith.yml <c>orchestrator:</c> block.
/// Peer to <c>IAgentImageResolver</c> — that one resolves the sandbox carrier,
/// this one resolves the pipeline-runner container the dispatcher spawns.
/// </summary>
public interface IOrchestratorImageResolver
{
    /// <summary>Returns "{registry}/{image-name}:{version}". Throws when no version is configured at either layer.</summary>
    string Resolve(ResolvedProject projectConfig);
}
