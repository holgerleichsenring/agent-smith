using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-11-26-462aa: a context's done.md — a short header, then one line per finished spec, newest first:
/// <c>- {id}: {entry}</c>. A line file rather than a YAML block, so that <c>merge=union</c> keeps the lines two
/// branches each added; the spec-first plugin's update-project migration writes the same header and lines.
/// </summary>
public static partial class ContextDoneIndex
{
    public const string Header =
        "# Done\n"
        + "\n"
        + "One line per finished spec, newest first: `- {id}: {what shipped, in what area} -> {pointer to the spec}`.\n"
        + "Search it for the area you touch; never read it front to back. Merged with merge=union (.gitattributes):\n"
        + "add or replace your own line, never reorder the others.\n";

    /// <summary>Every entry line's id and entry, in file order; a duplicate id is listed each time.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Entries(string? text) =>
    [
        .. Lines(text).Select(line => EntryLine().Match(line)).Where(m => m.Success)
            .Select(m => new KeyValuePair<string, string>(m.Groups["id"].Value, m.Groups["entry"].Value))
    ];

    /// <summary>
    /// The file with <paramref name="specId"/>'s line replaced in place, or inserted above the first entry — newest
    /// first. A second line for the id, as a union of two re-runs leaves it, is dropped. An absent file starts with
    /// the header.
    /// </summary>
    public static string Upsert(string? text, string specId, string entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);
        var line = Format(specId, entry);
        var lines = Lines(string.IsNullOrWhiteSpace(text) ? Header : text);
        var at = lines.FindIndex(l => IdOf(l) == specId);
        if (at >= 0)
        {
            lines[at] = line;
            for (var i = lines.Count - 1; i > at; i--) if (IdOf(lines[i]) == specId) lines.RemoveAt(i);
        }
        else
        {
            var first = lines.FindIndex(l => IdOf(l) is not null);
            if (first >= 0) lines.Insert(first, line);
            else lines.AddRange(lines.Count > 0 && lines[^1].Length > 0 ? [string.Empty, line] : [line]);
        }
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>The file with the <paramref name="older"/> entries it does not carry yet appended after its own —
    /// what a migration from context.yaml adds below the lines done.md already had.</summary>
    public static string Append(string? text, IEnumerable<KeyValuePair<string, string>> older)
    {
        ArgumentNullException.ThrowIfNull(older);
        text = string.IsNullOrWhiteSpace(text) ? Header : text;
        var lines = Lines(text);
        var have = Entries(text).Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var added = older.Where(e => have.Add(e.Key)).Select(e => Format(e.Key, e.Value)).ToList();
        if (added.Count == 0) return string.Join('\n', lines) + "\n";
        if (!lines.Any(l => IdOf(l) is not null) && lines.Count > 0 && lines[^1].Length > 0) lines.Add(string.Empty);
        lines.AddRange(added);
        return string.Join('\n', lines) + "\n";
    }

    private static string Format(string specId, string entry) => $"- {specId}: {entry}";

    private static string? IdOf(string line) => EntryLine().Match(line) is { Success: true } m ? m.Groups["id"].Value : null;

    private static List<string> Lines(string? text) =>
        [.. (text ?? string.Empty).Replace("\r\n", "\n").TrimEnd('\n').Split('\n')];

    [GeneratedRegex(@"^- (?<id>[^\s:]+): (?<entry>.*)$")]
    private static partial Regex EntryLine();
}
