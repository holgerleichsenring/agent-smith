using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Generates a .context.yaml (CCS format) for a repository using one cheap LLM call.
/// Implementations resolve IChatClient via IChatClientFactory using the supplied AgentConfig.
/// </summary>
public interface IContextGenerator
{
    Task<string> GenerateAsync(
        DetectedProject project,
        string repoPath,
        RepoSnapshot snapshot,
        AgentConfig agent,
        CancellationToken cancellationToken);

    Task<string> RetryWithErrorsAsync(
        DetectedProject project,
        string repoPath,
        string previousYaml,
        IReadOnlyList<string> validationErrors,
        AgentConfig agent,
        CancellationToken cancellationToken);
}
