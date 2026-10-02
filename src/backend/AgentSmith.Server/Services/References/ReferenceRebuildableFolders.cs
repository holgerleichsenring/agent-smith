namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-02-075da: the folders a manifest rebuilds — installed dependencies, caches, a VCS's
/// own store. Nothing authored lives in one, and one of them alone (a 47 MB .venv) outweighs the
/// set, so a file under any of them is left out and named. The dashboard mirrors this list to keep
/// a folder pick's body small, and a test holds the copy equal.
/// </summary>
public static class ReferenceRebuildableFolders
{
    public static readonly IReadOnlyList<string> Names =
    [
        ".git", "node_modules", ".venv", "venv", "__pycache__", ".pytest_cache", ".mypy_cache",
        ".next", ".nuxt", ".gradle", ".terraform", "bower_components",
    ];

    /// <summary>The rebuildable folder <paramref name="path"/> lies under, as its path prefix, or null.</summary>
    public static string? FolderOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('/');
        for (var i = 0; i < segments.Length - 1; i++)
            if (Names.Contains(segments[i], StringComparer.Ordinal))
                return string.Join('/', segments[..(i + 1)]) + "/";
        return null;
    }
}
