namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: what a file's path inside an uploaded set may be. It is normalised to forward
/// slashes and must stay inside the set: no absolute path, no drive, no '..', no NUL, no empty
/// segment, no longer than the column, and no second file under the same path in any case.
/// 2026-10-02-075da: a path that is left out is held to the HOSTILE half only — one that climbs
/// out is hostile whether or not it is stored, but a deep path under a .venv that is never stored
/// cannot overflow the column or collide with anything.
/// </summary>
public sealed class ReferencePathRule
{
    public string Normalise(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var forward = path.Replace('\\', '/');
        return forward.StartsWith("./", StringComparison.Ordinal) ? forward[2..] : forward;
    }

    /// <summary>Why <paramref name="path"/> is refused, or null when it is accepted.
    /// <paramref name="seen"/> collects the accepted paths of the set.</summary>
    public string? RefusalOf(string path, ISet<string> seen)
    {
        ArgumentNullException.ThrowIfNull(seen);
        if (HostileRefusalOf(path) is { } hostile) return hostile;
        if (path.Length > ReferenceUploadLimits.MaxPathChars)
            return $"'{path}' is longer than the {ReferenceUploadLimits.MaxPathChars}-character path limit.";
        return seen.Add(path) ? null : $"'{path}' appears twice in the set.";
    }

    /// <summary>Why <paramref name="path"/> would leave the set, or null when it stays inside it.</summary>
    public string? HostileRefusalOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Contains('\0')) return $"'{Shown(path)}' contains a NUL character.";
        if (path.StartsWith('/')) return $"'{path}' is an absolute path; a set holds relative paths only.";
        if (path.Length >= 2 && path[1] == ':') return $"'{path}' names a drive; a set holds relative paths only.";
        var segments = path.Split('/');
        if (segments.Contains("..")) return $"'{path}' climbs out of the set with '..'.";
        return segments.Any(s => s.Length == 0 || s == ".") ? $"'{path}' has an empty path segment." : null;
    }

    private static string Shown(string path) => path.Replace("\0", "\\0", StringComparison.Ordinal);
}
