using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Services.Orchestrator;

/// <summary>
/// Two-layer resolver: per-project OrchestratorConfig.Resources fully overrides
/// the global JobSpawnerOptions.Resources defaults when set. Partial overrides
/// are not supported — callers pick one layer wholesale. Lives in Server (not
/// Application) because JobSpawnerOptions — the global-default surface — is a
/// Server-side type that Application cannot reference; the interface stays in
/// Application so handlers can depend on the abstraction.
/// </summary>
public sealed class OrchestratorResourceResolver(IOptions<JobSpawnerOptions> options) : IOrchestratorResourceResolver
{
    public ResourceLimits Resolve(ProjectConfig projectConfig) =>
        projectConfig.Orchestrator?.Resources ?? options.Value.Resources;
}
