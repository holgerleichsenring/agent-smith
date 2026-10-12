using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0336: computes a run's COMPLETE pod footprint from the remote context
/// inventory — scoped repos read on the branches the run will read (2026-10-12-6bfaa) → toolchain-group sandboxes (each with its resolved
/// LIMIT) + the orchestrator — replacing the project.Repos.Count estimate that
/// mis-sized admission and discovered the real per-sandbox limits only mid-run.
/// </summary>
public interface IRunFootprintCalculator
{
    Task<RunFootprintBreakdown> CalculateAsync(
        ResolvedProject project, string? pipelineName, ContextReadBranches branches,
        CancellationToken cancellationToken);
}
