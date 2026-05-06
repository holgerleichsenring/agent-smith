using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Generates a code-map.yaml for a repository using one LLM call.
/// Maps modules, interfaces, implementations, key classes, and dependencies.
/// </summary>
public interface ICodeMapGenerator
{
    Task<string> GenerateAsync(
        DetectedProject project,
        string repoPath,
        RepoSnapshot snapshot,
        AgentConfig agent,
        CancellationToken cancellationToken);
}
