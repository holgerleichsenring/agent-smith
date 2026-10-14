using AgentSmith.Application.Services.Tools;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762n: where a cited block of lines stands now. Pure and exact: a range holds when its lines
/// still read the same (trailing whitespace aside); else the block is searched, and only a block found
/// EXACTLY ONCE moves — in its own file first, then in exactly one candidate file. Ranges keep their length.
/// </summary>
public static class SpecCitationLocator
{
    public const int MinSearchLineChars = 20;
    public const int MaxCandidates = 10;

    /// <summary>The text's lines with trailing whitespace trimmed (a final newline ends the last line).</summary>
    public static IReadOnlyList<string> Lines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();
        if (lines.Count > 1 && lines[^1].Length == 0 && text.EndsWith('\n')) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    /// <summary>The lines a range names, or null when it does not lie inside them.</summary>
    public static IReadOnlyList<string>? Block(IReadOnlyList<string> lines, EvidenceLineRange range) =>
        range.From < 1 || range.To < range.From || range.To > lines.Count ? null : [.. lines.Skip(range.From - 1).Take(range.To - range.From + 1)];

    /// <summary>Null when every range still holds; the new ranges when every block is found exactly once
    /// in <paramref name="current"/>; an EMPTY list (not found) otherwise.</summary>
    public static IReadOnlyList<EvidenceLineRange>? InFile(
        IReadOnlyList<IReadOnlyList<string>> blocks, IReadOnlyList<EvidenceLineRange> ranges, string current)
    {
        var lines = Lines(current);
        var placed = new List<EvidenceLineRange>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (Block(lines, ranges[i]) is { } now && now.SequenceEqual(blocks[i], StringComparer.Ordinal)) placed.Add(ranges[i]);
            else if (Once(blocks[i], lines) is { } found) placed.Add(found);
            else return [];
        }
        return placed.SequenceEqual(ranges) ? null : placed;
    }

    /// <summary>The one candidate holding every block exactly once, with its ranges; null when none or several.</summary>
    public static (string Path, IReadOnlyList<EvidenceLineRange> Ranges)? InOne(
        IReadOnlyList<IReadOnlyList<string>> blocks, IEnumerable<(string Path, string Text)> candidates)
    {
        var hits = candidates
            .Select(c => (c.Path, Ranges: blocks.Select(b => Once(b, Lines(c.Text))).ToList()))
            .Where(c => c.Ranges.All(r => r is not null)).Take(2).ToList();
        return hits.Count == 1 ? (hits[0].Path, [.. hits[0].Ranges.Select(r => r!)]) : null;
    }

    /// <summary>The files to try, in order: each build-note destination, then each grep hit; the cited file
    /// itself never; null when more than <see cref="MaxCandidates"/> remain.</summary>
    public static IReadOnlyList<string>? Candidates(IEnumerable<string> noteDestinations, IEnumerable<string> grepped, string cited)
    {
        var ordered = noteDestinations.Concat(grepped).Where(p => p != cited).Distinct(StringComparer.Ordinal).ToList();
        return ordered.Count > MaxCandidates ? null : ordered;
    }

    /// <summary>The new side of every preceding note's moved entry whose old side is <paramref name="cited"/>.</summary>
    public static IEnumerable<string> NoteDestinations(IReadOnlyList<PrecedingNote> notes, string cited) =>
        notes.SelectMany(n => n.Note?.Moved ?? [])
            .Select(e => e.Split(HandForwardToolHost.Arrow, 2))
            .Where(p => p.Length == 2 && Plain(p[0]) == Plain(cited))
            .Select(p => Plain(p[1]));

    /// <summary>The longest trimmed line of the blocks, at least <see cref="MinSearchLineChars"/> long; null when none is.</summary>
    public static string? SearchLine(IEnumerable<IReadOnlyList<string>> blocks) =>
        blocks.SelectMany(b => b).Select(l => l.Trim()).Where(l => l.Length >= MinSearchLineChars)
            .OrderByDescending(l => l.Length).FirstOrDefault();

    private static EvidenceLineRange? Once(IReadOnlyList<string> block, IReadOnlyList<string> lines)
    {
        EvidenceLineRange? found = null;
        for (var start = 0; start + block.Count <= lines.Count; start++)
        {
            if (!Matches(block, lines, start)) continue;
            if (found is not null) return null;
            found = new EvidenceLineRange(start + 1, start + block.Count);
        }
        return found;
    }

    private static bool Matches(IReadOnlyList<string> block, IReadOnlyList<string> lines, int start) =>
        Enumerable.Range(0, block.Count).All(i => string.Equals(block[i], lines[start + i], StringComparison.Ordinal));

    private static string Plain(string path) => SeriesContractSharedFiles.Normalize(path.Trim('`', '"', '\''));
}
