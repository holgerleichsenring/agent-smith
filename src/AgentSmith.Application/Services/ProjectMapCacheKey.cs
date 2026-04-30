using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Application.Services;

/// <summary>
/// Computes a content hash over a repository's dependency manifests to use
/// as the ProjectMap cache key. Changes outside manifests don't invalidate
/// (the structural shape stays the same); a manifest edit re-runs the analyzer.
/// </summary>
public static class ProjectMapCacheKey
{
    private static readonly string[] ManifestPatterns =
    [
        "*.csproj", "*.sln", "package.json", "pyproject.toml", "requirements.txt",
        "go.mod", "Cargo.toml", "pom.xml", "build.gradle", "build.gradle.kts"
    ];

    public static string Compute(string repoPath)
    {
        if (!Directory.Exists(repoPath)) return string.Empty;

        var manifests = ManifestPatterns
            .SelectMany(pattern =>
            {
                try { return Directory.GetFiles(repoPath, pattern, SearchOption.AllDirectories); }
                catch { return Array.Empty<string>(); }
            })
            .Where(f => !IsExcludedPath(f, repoPath))
            .Select(f => (RelPath: NormalizeRelative(f, repoPath), FullPath: f))
            .OrderBy(t => t.RelPath, StringComparer.Ordinal)
            .ToList();

        if (manifests.Count == 0) return string.Empty;

        using var sha = SHA256.Create();
        using var ms = new MemoryStream();
        foreach (var (rel, full) in manifests)
        {
            ms.Write(Encoding.UTF8.GetBytes(rel + "\n"));
            try { ms.Write(File.ReadAllBytes(full)); }
            catch { /* best-effort */ }
            ms.Write([0x1E]); // record separator
        }
        ms.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(ms));
    }

    private static string NormalizeRelative(string fullPath, string repoPath) =>
        Path.GetRelativePath(repoPath, fullPath).Replace('\\', '/');

    private static readonly string[] ExcludedSegments =
        ["bin", "obj", "node_modules", ".git"];

    private static bool IsExcludedPath(string fullPath, string repoPath)
    {
        var segments = NormalizeRelative(fullPath, repoPath).Split('/');
        return segments.Any(s => ExcludedSegments.Contains(s, StringComparer.OrdinalIgnoreCase));
    }
}
