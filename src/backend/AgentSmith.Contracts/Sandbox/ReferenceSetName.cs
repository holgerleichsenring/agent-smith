namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-01-283db: what an uploaded set is called — the folder all its paths share, which a
/// folder upload or an unpacked archive puts every file under.
/// 2026-10-01-283df: moved here from the set repository, because a run names the sets it carries
/// from the same paths and two derivations of one name would be two answers.
/// 2026-10-09-86e1: and when they share none, the one file's name, else "&lt;first file&gt; + N more"
/// — never the word "site". The name is a label and an address only: a run writes a set under a
/// directory keyed by its id, so a new name moves no file.
/// </summary>
public static class ReferenceSetName
{
    public static string Of(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var sorted = paths.Order(StringComparer.Ordinal).ToList();
        var tops = sorted.Select(p => p.IndexOf('/') is var slash and > 0 ? p[..slash] : null).Distinct().ToList();
        if (tops is [{ } only]) return only;
        if (sorted.Count == 0) return string.Empty;
        var first = sorted[0][(sorted[0].LastIndexOf('/') + 1)..];
        return sorted.Count == 1 ? first : $"{first} + {sorted.Count - 1} more";
    }
}
