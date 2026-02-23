using AgentSmith.Contracts.Models;

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
        CancellationToken cancellationToken);
}
