using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Tool-driven repository analyzer. Replaces the heuristic
/// RepoSnapshotCollector + CodeMapGenerator stack with an agentic loop that
/// produces a structured ProjectMap.
/// </summary>
public interface IProjectAnalyzer
{
    Task<ProjectMap> AnalyzeAsync(
        string repositoryPath,
        AgentConfig agent,
        CancellationToken cancellationToken);
}
