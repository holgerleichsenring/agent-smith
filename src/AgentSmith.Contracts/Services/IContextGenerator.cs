using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Generates a .context.yaml (CCS format) for a repository using one cheap LLM call.
/// </summary>
public interface IContextGenerator
{
    Task<string> GenerateAsync(
        DetectedProject project,
        string repoPath,
        RepoSnapshot? snapshot = null,
        CancellationToken cancellationToken = default);

    Task<string> RetryWithErrorsAsync(
        DetectedProject project,
        string repoPath,
        string previousYaml,
        IReadOnlyList<string> validationErrors,
        CancellationToken cancellationToken = default);
}
