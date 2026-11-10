namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-09-0620k: which listed directories a review never opened a file under — the areas a coverage nudge names.
/// The repository root is not an area; a directory whose subdirectory was also listed gives way to it.
/// </summary>
internal static class ListedAreas
{
    internal static IReadOnlyList<string> Unread(IEnumerable<string> listed, IEnumerable<string> read, object sync)
    {
        string[] dirs, files;
        lock (sync) { dirs = [.. listed.Select(Normal)]; files = [.. read.Select(Normal)]; }
        return [.. dirs
            .Where(d => d.Length > 0)
            .Where(d => !dirs.Any(other => other.Length > d.Length && other.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase)))
            .Where(d => !files.Any(f => f.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)];
    }

    private static string Normal(string path)
    {
        var p = path.Replace('\\', '/').Trim().TrimEnd('/');
        while (p.StartsWith("./", StringComparison.Ordinal)) p = p[2..];
        return p == "." ? string.Empty : p.TrimStart('/');
    }
}
