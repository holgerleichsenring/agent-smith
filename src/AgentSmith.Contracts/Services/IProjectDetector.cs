using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Deterministic project detector that scans a repository root and returns
/// language, stack, build/test commands without using any LLM tokens.
/// </summary>
public interface IProjectDetector
{
    DetectedProject Detect(string repoPath);
}
