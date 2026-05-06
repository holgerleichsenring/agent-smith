using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services;

/// <summary>
/// Computes a content hash over a repository's dependency manifests to use
/// as the ProjectMap cache key. Walks the sandbox-side filesystem via
/// ISandboxFileReader so the hash matches what the analyzer actually saw.
/// </summary>
public static class ProjectMapCacheKey
{
    private const int MaxSearchDepth = 6;

    private static readonly HashSet<string> ManifestNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "pyproject.toml", "requirements.txt",
        "go.mod", "Cargo.toml", "pom.xml",
        "build.gradle", "build.gradle.kts"
    };

    private static readonly string[] ManifestExtensions = [".csproj", ".sln"];

    public static async Task<string> ComputeAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        var entries = await reader.ListAsync(repoPath, MaxSearchDepth, cancellationToken);
        var manifests = entries
            .Where(IsManifest)
            .Where(p => !ContainsExcludedSegment(p, repoPath))
            .Select(f => (RelPath: NormalizeRelative(f, repoPath), FullPath: f))
            .OrderBy(t => t.RelPath, StringComparer.Ordinal)
            .ToList();

        if (manifests.Count == 0) return string.Empty;

        using var sha = SHA256.Create();
        using var ms = new MemoryStream();
        foreach (var (rel, full) in manifests)
        {
            ms.Write(Encoding.UTF8.GetBytes(rel + "\n"));
            var content = await reader.TryReadAsync(full, cancellationToken);
            if (content is not null)
                ms.Write(Encoding.UTF8.GetBytes(content));
            ms.Write([0x1E]);
        }
        ms.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(ms));
    }

    private static bool IsManifest(string path)
    {
        var name = LastSegment(path);
        if (ManifestNames.Contains(name)) return true;
        foreach (var ext in ManifestExtensions)
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string NormalizeRelative(string fullPath, string repoPath)
    {
        var rel = fullPath.Length > repoPath.Length ? fullPath[repoPath.Length..] : fullPath;
        return rel.TrimStart('/').Replace('\\', '/');
    }

    private static bool ContainsExcludedSegment(string fullPath, string repoPath)
    {
        var rel = NormalizeRelative(fullPath, repoPath);
        var segments = rel.Split('/');
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var seg = segments[i];
            if (seg.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals(".git", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }
}
