using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Deterministic project detector that scans a repository root and returns
/// language, stack, build/test commands without using any LLM tokens. Reads
/// flow through ISandboxFileReader so detection runs against the sandbox.
/// </summary>
public interface IProjectDetector
{
    Task<DetectedProject> DetectAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken);
}
