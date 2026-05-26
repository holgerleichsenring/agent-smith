namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Host-disk variant of the .gitignore resolver — used by api-scan code analyzers
/// when the source tree is on the local filesystem (--source-path bind-mount).
/// Uses the Ignore NuGet package; no LibGit2Sharp / native libgit2 dependency.
/// </summary>
internal sealed class HostGitIgnore
{
    private readonly Ignore.Ignore _ignore = new();
    private readonly bool _hasAny;

    private HostGitIgnore(bool hasAny) => _hasAny = hasAny;

    public static HostGitIgnore Load(string repoPath)
    {
        var path = Path.Combine(repoPath, ".gitignore");
        if (!File.Exists(path))
            return new HostGitIgnore(hasAny: false);

        var instance = new HostGitIgnore(hasAny: true);
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;
            instance._ignore.Add(trimmed);
        }
        return instance;
    }

    public bool IsIgnored(string fullPath, string repoPath)
    {
        if (!_hasAny) return false;
        var rel = Path.GetRelativePath(repoPath, fullPath).Replace('\\', '/');
        return _ignore.IsIgnored(rel);
    }
}
