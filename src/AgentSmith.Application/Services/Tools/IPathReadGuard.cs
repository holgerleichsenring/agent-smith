namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Validates that a path is readable for a skill: inside the repo root, not
/// .gitignored, not under .git/. Wraps the IGitIgnoreResolver contract.
/// Stateless — the repo root is passed per call so the guard can be a
/// singleton.
/// </summary>
public interface IPathReadGuard
{
    Result AssertReadable(string path, string repoRoot);
}
