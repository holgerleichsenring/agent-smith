using LibGit2Sharp;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Wraps LibGit2Sharp to answer "is this path gitignored?" for a scan root.
/// Returns false (not ignored) when the scan root is not inside a git repo —
/// the caller then falls back to its hardcoded exclude list.
/// Not thread-safe: LibGit2Sharp Repository instances must be used from a single thread.
/// </summary>
internal sealed class GitIgnoreResolver : IDisposable
{
    private readonly Repository? _repo;
    private readonly string _workingDir;

    public GitIgnoreResolver(string repoPath)
    {
        var gitDir = Repository.Discover(repoPath);
        if (gitDir is null)
        {
            _repo = null;
            _workingDir = repoPath;
            return;
        }

        _repo = new Repository(gitDir);
        _workingDir = _repo.Info.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar) ?? repoPath;
    }

    public bool IsGitRepo => _repo is not null;

    public bool IsIgnored(string absolutePath)
    {
        if (_repo is null) return false;

        var relative = ToRelative(_workingDir, absolutePath);
        if (relative is null) return false;

        relative = relative.Replace(Path.DirectorySeparatorChar, '/');
        return _repo.Ignore.IsPathIgnored(relative);
    }

    public void Dispose() => _repo?.Dispose();

    // LibGit2Sharp resolves realpath (on macOS turns /var/... into /private/var/...).
    // Callers may still supply the pre-symlink path, so try both forms before giving up.
    private static string? ToRelative(string workingDir, string absolutePath)
    {
        var direct = Path.GetRelativePath(workingDir, absolutePath);
        if (!direct.StartsWith("..", StringComparison.Ordinal))
            return direct;

        var normalized = Path.GetRelativePath(
            StripPrivatePrefix(workingDir), StripPrivatePrefix(absolutePath));
        return normalized.StartsWith("..", StringComparison.Ordinal) ? null : normalized;
    }

    private static string StripPrivatePrefix(string path) =>
        path.StartsWith("/private/", StringComparison.Ordinal) ? path[8..] : path;
}
