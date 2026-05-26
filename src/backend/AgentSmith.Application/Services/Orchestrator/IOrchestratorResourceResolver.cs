using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Orchestrator;

/// <summary>
/// Resolves the effective <see cref="ResourceLimits"/> for the orchestrator
/// container by walking the override chain: projects.&lt;name&gt;.orchestrator.resources
/// (per-project) wins; otherwise the global <c>JobSpawner:Resources</c> defaults
/// apply. Peer to <c>ISandboxResourceResolver</c> — that one resolves the
/// sandbox toolchain container, this one resolves the orchestrator.
/// </summary>
public interface IOrchestratorResourceResolver
{
    ResourceLimits Resolve(ResolvedProject projectConfig);
}
