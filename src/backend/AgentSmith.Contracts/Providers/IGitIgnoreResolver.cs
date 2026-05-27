namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Answers "is this absolute path .gitignored under the given repo root?".
/// Implementations load the repo's .gitignore at construction time. Used by
/// PathReadGuard to decide whether a tool may read a path.
/// </summary>
public interface IGitIgnoreResolver
{
    bool IsIgnored(string fullPath, string repoPath);
}
