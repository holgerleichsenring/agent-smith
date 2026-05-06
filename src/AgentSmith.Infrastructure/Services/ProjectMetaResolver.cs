using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Finds .agentsmith/ under a source path via ISandboxFileReader. Repo-root
/// first, then the depth-first lexical descent (mono-repos with .agentsmith/
/// in a sub-package). One ListAsync call covers the whole search tree —
/// per-candidate ExistsAsync probes are avoided to minimise round trips.
/// </summary>
public sealed class ProjectMetaResolver : IProjectMetaResolver
{
    private const string MetaDirName = ".agentsmith";
    private const int MaxSearchDepth = 4;

    public async Task<string?> ResolveAsync(
        ISandboxFileReader reader, string sourcePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return null;

        var entries = await reader.ListAsync(sourcePath, MaxSearchDepth, cancellationToken);
        if (entries.Count == 0) return null;

        var rootCandidate = NormaliseRoot(sourcePath) + MetaDirName;
        var rootHit = entries.FirstOrDefault(p => p.Equals(rootCandidate, StringComparison.Ordinal));
        if (rootHit is not null) return rootHit;

        return entries
            .Where(p => GetFileName(p).Equals(MetaDirName, StringComparison.Ordinal))
            .Where(p => !ContainsSkippedSegment(p, sourcePath))
            .OrderBy(p => GetDepth(p, sourcePath))
            .ThenBy(p => p, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static string NormaliseRoot(string path) =>
        path.EndsWith('/') ? path : path + "/";

    private static string GetFileName(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }

    private static int GetDepth(string entry, string root)
    {
        var rel = entry.Length > root.Length ? entry[root.Length..] : string.Empty;
        return rel.Count(c => c == '/');
    }

    private static bool ContainsSkippedSegment(string entry, string root)
    {
        var rel = entry.Length > root.Length ? entry[root.Length..].TrimStart('/') : entry;
        var segments = rel.Split('/');
        for (var i = 0; i < segments.Length - 1; i++)
            if (IsSkippedSegment(segments[i])) return true;
        return false;
    }

    private static bool IsSkippedSegment(string name) =>
        name.StartsWith('.')
        || name.Equals("node_modules", StringComparison.Ordinal)
        || name.Equals("bin", StringComparison.Ordinal)
        || name.Equals("obj", StringComparison.Ordinal)
        || name.Equals("dist", StringComparison.Ordinal)
        || name.Equals("target", StringComparison.Ordinal)
        || name.Equals("build", StringComparison.Ordinal)
        || name.Equals("vendor", StringComparison.Ordinal);
}
