using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Finds .agentsmith/ under a source path. Repo-root first, then a single
/// depth-first lexical descent so mono-repos with .agentsmith/ in a sub-package
/// (services/api-gateway/.agentsmith/, packages/core/.agentsmith/, …) resolve.
/// First hit wins; subsequent .agentsmith/ directories are ignored.
/// </summary>
public sealed class ProjectMetaResolver : IProjectMetaResolver
{
    private const string MetaDirName = ".agentsmith";
    private const int MaxSearchDepth = 4;

    public string? Resolve(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
            return null;

        var rootCandidate = Path.Combine(sourcePath, MetaDirName);
        if (Directory.Exists(rootCandidate))
            return rootCandidate;

        return SearchRecursive(sourcePath, depth: 0);
    }

    private static string? SearchRecursive(string current, int depth)
    {
        if (depth >= MaxSearchDepth) return null;

        IEnumerable<string> subDirs;
        try { subDirs = Directory.EnumerateDirectories(current).OrderBy(p => p, StringComparer.Ordinal); }
        catch { return null; }

        foreach (var dir in subDirs)
        {
            if (ShouldSkip(dir)) continue;

            var candidate = Path.Combine(dir, MetaDirName);
            if (Directory.Exists(candidate)) return candidate;

            var nested = SearchRecursive(dir, depth + 1);
            if (nested is not null) return nested;
        }

        return null;
    }

    private static bool ShouldSkip(string dir)
    {
        var name = Path.GetFileName(dir);
        return name.StartsWith('.')
            || name.Equals("node_modules", StringComparison.Ordinal)
            || name.Equals("bin", StringComparison.Ordinal)
            || name.Equals("obj", StringComparison.Ordinal)
            || name.Equals("dist", StringComparison.Ordinal)
            || name.Equals("target", StringComparison.Ordinal)
            || name.Equals("build", StringComparison.Ordinal)
            || name.Equals("vendor", StringComparison.Ordinal);
    }
}
