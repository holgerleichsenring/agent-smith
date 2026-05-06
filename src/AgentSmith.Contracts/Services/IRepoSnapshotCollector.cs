using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Collects raw repository data (config files, code samples) for LLM
/// interpretation. Reads via ISandboxFileReader so collection runs against
/// the sandbox /work tree. Pure data collection — no interpretation, no LLM.
/// </summary>
public interface IRepoSnapshotCollector
{
    Task<RepoSnapshot> CollectAsync(
        ISandboxFileReader reader,
        string repoPath,
        DetectedProject project,
        CancellationToken cancellationToken);
}
