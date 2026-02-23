using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Collects raw repository data (config files, code samples) for LLM interpretation.
/// Pure filesystem reads, zero interpretation, zero LLM tokens.
/// </summary>
public interface IRepoSnapshotCollector
{
    RepoSnapshot Collect(string repoPath, DetectedProject project);
}
