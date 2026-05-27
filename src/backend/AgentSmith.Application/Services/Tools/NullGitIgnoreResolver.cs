using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// IGitIgnoreResolver that never ignores. DI default — bootstrap and other
/// phases that don't need .gitignore awareness use this. Implementation
/// phase wires the real Infrastructure.GitIgnoreResolver via per-pipeline
/// factory call when applicable.
/// </summary>
public sealed class NullGitIgnoreResolver : IGitIgnoreResolver
{
    public bool IsIgnored(string fullPath, string repoPath) => false;
}
