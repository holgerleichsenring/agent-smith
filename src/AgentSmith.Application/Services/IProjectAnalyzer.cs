using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Tool-driven repository analyzer. Drives the LLM through the SandboxToolHost
/// scout subset (read/list/grep) and parses the model's terminal JSON response
/// into a ProjectMap.
/// </summary>
public interface IProjectAnalyzer
{
    Task<ProjectMap> AnalyzeAsync(
        string repositoryPath,
        AgentConfig agent,
        ISandbox sandbox,
        CancellationToken cancellationToken);
}
