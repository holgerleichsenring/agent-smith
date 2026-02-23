using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Generates a coding-principles.md for a repository using one LLM call.
/// Analyzes code samples and config files to detect style, naming, patterns, and methodology.
/// </summary>
public interface ICodingPrinciplesGenerator
{
    Task<string> GenerateAsync(
        DetectedProject project,
        string repoPath,
        RepoSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
