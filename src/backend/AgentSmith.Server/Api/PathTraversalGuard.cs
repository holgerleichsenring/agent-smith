namespace AgentSmith.Server.Api;

/// <summary>
/// p0169a: rejects absolute paths and any relative path that escapes the
/// allowed root via canonicalisation comparison. The dashboard's
/// /api/jobs/{id}/files/{*path} relies on this — any path that resolves
/// outside the run directory after Path.GetFullPath() is rejected with 400.
/// </summary>
public static class PathTraversalGuard
{
    public static bool TryResolveWithin(string root, string relative, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relative)) return false;
        if (Path.IsPathRooted(relative)) return false;
        if (relative.Contains("..", StringComparison.Ordinal))
        {
            // Allow if the canonical form still lives under root, but reject
            // any segment that explicitly escapes — covers ../, ../../, ./../
            var canonRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var canonCandidate = Path.GetFullPath(Path.Combine(root, relative));
            if (!canonCandidate.StartsWith(canonRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !string.Equals(canonCandidate, canonRoot, StringComparison.Ordinal))
                return false;
            fullPath = canonCandidate;
            return File.Exists(fullPath);
        }

        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var combined = Path.GetFullPath(Path.Combine(root, relative));
        if (!combined.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return false;
        fullPath = combined;
        return File.Exists(fullPath);
    }
}
