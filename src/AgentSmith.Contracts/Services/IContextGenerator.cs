using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Generates a .context.yaml (CCS format) for a repository using one cheap LLM call.
/// Implementations resolve IChatClient via IChatClientFactory using the supplied AgentConfig.
/// Reads key files via ISandboxFileReader so generation runs against the sandbox /work tree.
/// </summary>
public interface IContextGenerator
{
    Task<string> GenerateAsync(
        ISandboxFileReader reader,
        DetectedProject project,
        string repoPath,
        RepoSnapshot snapshot,
        AgentConfig agent,
        CancellationToken cancellationToken);

    Task<string> RetryWithErrorsAsync(
        DetectedProject project,
        string previousYaml,
        IReadOnlyList<string> validationErrors,
        AgentConfig agent,
        CancellationToken cancellationToken);
}
