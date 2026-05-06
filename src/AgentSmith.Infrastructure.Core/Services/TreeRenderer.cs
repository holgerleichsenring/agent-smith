namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Renders a flat list of absolute path entries (as produced by ISandboxFileReader.ListAsync)
/// as an indented tree relative to a root, skipping any entry whose ancestor segment is in
/// the exclude set. Used by RepoSnapshotCollector to feed prompt-builders a directory overview.
/// </summary>
internal static class TreeRenderer
{
    public static List<string> Render(
        string rootPath,
        IReadOnlyList<string> entries,
        int maxDepth,
        ISet<string> excludedDirs)
    {
        var rooted = entries
            .Select(e => RelativeOf(e, rootPath))
            .Where(rel => !string.IsNullOrEmpty(rel))
            .Where(rel => !rel.Split('/').Any(excludedDirs.Contains))
            .OrderBy(rel => rel, StringComparer.Ordinal)
            .ToList();

        var dirs = new HashSet<string>(rooted.Where(r => entries.Contains(rootPath + "/" + r))
            .DefaultIfEmpty(string.Empty));

        var lines = new List<string>();
        foreach (var rel in rooted)
        {
            var depth = rel.Count(c => c == '/');
            if (depth >= maxDepth) continue;

            var indent = new string(' ', depth * 2);
            var name = LastSegment(rel);
            var isDir = entries.Any(e => e.StartsWith(rootPath + "/" + rel + "/", StringComparison.Ordinal));
            lines.Add($"{indent}{name}{(isDir ? "/" : "")}");
        }
        return lines;
    }

    private static string RelativeOf(string fullPath, string root)
    {
        var rel = fullPath.Length > root.Length ? fullPath[root.Length..] : string.Empty;
        return rel.TrimStart('/');
    }

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }
}
