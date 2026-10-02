using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-02-075da: the set's own bounds over what is KEPT — a file count and a total size. A
/// refusal names the largest top-level entries, so the operator sees what to leave out rather than
/// which file happened to cross the line.
/// </summary>
public static class ReferenceSetBounds
{
    private const int LargestNamed = 3;

    public static string? RefusalOf(IReadOnlyList<ReferenceUploadPart> kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var total = kept.Sum(p => p.Content.LongLength);
        if (kept.Count > ReferenceUploadLimits.MaxFiles)
            return $"The upload keeps {kept.Count} files, over the {ReferenceUploadLimits.MaxFiles}-file limit; "
                + $"the most files are in {Largest(kept, p => 1)}.";
        return total > ReferenceUploadLimits.MaxSetBytes
            ? $"The upload keeps {ReferenceUploadLimits.Megabytes(total)}, over the "
                + $"{ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxSetBytes)} set limit; the largest entries are "
                + $"{Largest(kept, p => p.Content.LongLength, ReferenceUploadLimits.Megabytes)}."
            : null;
    }

    private static string Largest(
        IReadOnlyList<ReferenceUploadPart> kept, Func<ReferenceUploadPart, long> weight, Func<long, string>? shown = null)
    {
        var root = Root(kept);
        return string.Join(", ", kept.GroupBy(p => TopLevel(p.Path, root), StringComparer.Ordinal)
            .Select(g => (Entry: g.Key, Weight: g.Sum(weight)))
            .OrderByDescending(e => e.Weight).ThenBy(e => e.Entry, StringComparer.Ordinal).Take(LargestNamed)
            .Select(e => $"'{e.Entry}' ({(shown is null ? $"{e.Weight} files" : shown(e.Weight))})"));
    }

    // A folder pick puts every path under the folder's own name; the entries are what is inside it.
    private static string? Root(IReadOnlyList<ReferenceUploadPart> kept) =>
        kept.Select(p => p.Path.IndexOf('/') is var slash and > 0 ? p.Path[..slash] : null).Distinct().ToList()
            is [{ } only] ? only : null;

    private static string TopLevel(string path, string? root)
    {
        var rest = root is null ? path : path[(root.Length + 1)..];
        var slash = rest.IndexOf('/');
        var entry = slash > 0 ? rest[..(slash + 1)] : rest;
        return root is null ? entry : $"{root}/{entry}";
    }
}
