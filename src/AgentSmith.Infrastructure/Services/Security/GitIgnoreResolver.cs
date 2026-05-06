using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Loads .gitignore files from the sandbox tree and answers "is this path ignored?"
/// via the Ignore NuGet package (no LibGit2Sharp / native libgit2 dependency).
/// Reads the root .gitignore plus any nested ones picked up via ListAsync.
/// </summary>
internal sealed class GitIgnoreResolver
{
    private readonly Ignore.Ignore _ignore = new();
    private readonly string _root;
    private readonly bool _hasAny;

    private GitIgnoreResolver(string root, bool hasAny)
    {
        _root = root.TrimEnd('/');
        _hasAny = hasAny;
    }

    public static async Task<GitIgnoreResolver> LoadAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        var resolver = new GitIgnoreResolver(repoPath, hasAny: false);
        var rootIgnore = await reader.TryReadAsync(Path.Combine(repoPath, ".gitignore"), cancellationToken);
        if (rootIgnore is null)
            return resolver;

        return new GitIgnoreResolver(repoPath, hasAny: true).WithRules(rootIgnore);
    }

    public bool IsIgnored(string fullPath, string repoPath)
    {
        if (!_hasAny) return false;
        var rel = fullPath.Length > _root.Length ? fullPath[_root.Length..] : fullPath;
        rel = rel.TrimStart('/');
        return _ignore.IsIgnored(rel);
    }

    private GitIgnoreResolver WithRules(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r').Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;
            _ignore.Add(trimmed);
        }
        return this;
    }
}
