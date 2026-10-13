namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: one reading of a context's meta.workdir for every structure caller, so the
/// init that writes a baseline and the gate that reads it assign a file to the same context.
/// '.', './' and a trailing slash name the repository root; a rooted or escaping workdir names
/// nothing a repository-relative path can sit under.
/// </summary>
public static class StructureWorkdir
{
    /// <summary>The repository-relative directory, "" for the root, or null when rooted or escaping.</summary>
    public static string? Normalize(string? workdir)
    {
        var raw = (workdir ?? ".").Replace('\\', '/').Trim();
        if (raw.StartsWith('/') || Path.IsPathRooted(raw)) return null;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(p => p != ".").ToList();
        return parts.Contains("..") ? null : string.Join('/', parts);
    }

    public static bool Contains(string normalized, string path) =>
        normalized.Length == 0
        || path.StartsWith(normalized + "/", StringComparison.Ordinal);

    /// <summary>The longest normalized workdir among <paramref name="workdirs"/> holding the path.</summary>
    public static string? Owner(IEnumerable<string> workdirs, string path) =>
        workdirs.Where(w => Contains(w, path)).OrderByDescending(w => w.Length).FirstOrDefault();
}
