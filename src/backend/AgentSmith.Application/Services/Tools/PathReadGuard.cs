using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Validates that a path is readable for a skill. Three rejection modes:
/// path-outside-repo (escapes the working repo root, including via .. or symlinks),
/// path-gitignored (.gitignore match), and path-in-dot-git (inside the .git/ tree).
/// Returns a structured <see cref="Result"/>; the runtime turns the error into a
/// tool-result string that flows back into the LLM loop.
/// </summary>
public sealed class PathReadGuard : IPathReadGuard
{
    private const string DotGitSegment = ".git";

    private readonly IGitIgnoreResolver _gitIgnore;
    private readonly Func<string> _repoRootProvider;

    public PathReadGuard(IGitIgnoreResolver gitIgnore, Func<string> repoRootProvider)
    {
        _gitIgnore = gitIgnore;
        _repoRootProvider = repoRootProvider;
    }

    public Result AssertReadable(string path)
    {
        var repoRoot = NormalizeRoot(_repoRootProvider());
        var resolved = ResolveAgainstRoot(path, repoRoot);

        if (!IsInsideRoot(resolved, repoRoot))
            return Fail(GuardErrorKind.OutsideRepo, path, $"path '{path}' is outside the repository root");

        if (IsInDotGit(resolved, repoRoot))
            return Fail(GuardErrorKind.InDotGit, path, $"path '{path}' is inside the .git directory");

        if (_gitIgnore.IsIgnored(resolved, repoRoot))
            return Fail(GuardErrorKind.GitIgnored, path, $"path '{path}' is .gitignored");

        return Result.Ok();
    }

    private static Result Fail(GuardErrorKind kind, string path, string message)
        => Result.Fail(new GuardError { Kind = kind, Path = path, Message = message });

    private static string NormalizeRoot(string root) => root.TrimEnd('/');

    private static string ResolveAgainstRoot(string path, string root)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);
        return Path.GetFullPath(Path.Combine(root, path));
    }

    private static bool IsInsideRoot(string resolvedPath, string root)
    {
        var rootWithSep = root.EndsWith('/') ? root : root + '/';
        return resolvedPath == root
            || resolvedPath.StartsWith(rootWithSep, StringComparison.Ordinal);
    }

    private static bool IsInDotGit(string resolvedPath, string root)
    {
        var rel = resolvedPath.Length > root.Length ? resolvedPath[root.Length..].TrimStart('/') : "";
        return rel == DotGitSegment
            || rel.StartsWith(DotGitSegment + "/", StringComparison.Ordinal);
    }
}
