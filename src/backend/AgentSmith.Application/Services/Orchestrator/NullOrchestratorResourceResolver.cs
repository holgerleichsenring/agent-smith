using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Orchestrator;

/// <summary>
/// p0320b: default resolver for compositions that spawn no orchestrator pod —
/// CLI / in-process runs execute the pipeline in the current process, so the
/// run footprint carries zero orchestrator resources. The Server composition
/// replaces this with the JobSpawnerOptions-backed resolver.
/// </summary>
public sealed class NullOrchestratorResourceResolver : IOrchestratorResourceResolver
{
    public ResourceLimits? Resolve(ResolvedProject projectConfig) => null;
}
