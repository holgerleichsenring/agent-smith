namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-11-24-6811b: resolves a MEMORY.md two branches each changed — the conflict every pair of parallel specs that
/// remember something produces, since both append to the end. Entry lines are keyed by name and compared without their
/// label: per name, equal sides keep it, a side equal to base takes the other's change or removal, two different changes
/// refuse. The label comes from the side that changed it, ours when both did — the next record step rewrites every label
/// from evidence anyway. The header block before the first entry must be equal or one side equal to base; a non-blank line
/// that is no entry after the first entry refuses. The result is ours in its order with theirs' new entries after it.
/// </summary>
public static class MemoryIndexUnion
{
    /// <summary>The united index, or null when the conflict is more than that; null for a side that deleted the file.</summary>
    public static string? Resolve(string? @base, string? ours, string? theirs)
    {
        if (ours is null || theirs is null) return null;
        if (Parse(@base ?? string.Empty) is not { } b || Parse(ours) is not { } o || Parse(theirs) is not { } t) return null;
        if (Pick(Join(b.Header), Join(o.Header), Join(t.Header)) is not { Found: true } header) return null;
        var lines = new List<string>();
        if (header.Value is { Length: > 0 } text) lines.AddRange(text.Split('\n'));
        foreach (var name in o.Order.Concat(t.Order.Where(n => !o.Entries.ContainsKey(n))))
        {
            if (Entry(b.Entries.GetValueOrDefault(name), o.Entries.GetValueOrDefault(name), t.Entries.GetValueOrDefault(name))
                is not { Found: true } entry) return null;
            if (entry.Value is { } line) lines.Add(line);
        }
        return string.Join('\n', lines) + (ours.EndsWith('\n') ? "\n" : string.Empty);
    }

    private static (bool Found, string? Value) Entry(Line? b, Line? o, Line? t)
    {
        var content = Pick(b?.Parsed.Format(null), o?.Parsed.Format(null), t?.Parsed.Format(null));
        if (!content.Found) return (false, null);
        if (content.Value is null) return (true, null);
        var side = content.Value == o?.Parsed.Format(null) ? o! : t!;
        var label = Pick(b?.Parsed.Label, o?.Parsed.Label, t?.Parsed.Label) is { Found: true } one ? one.Value : o?.Parsed.Label;
        // A line whose label stays is kept as written; only a label taken from the other side is formatted.
        return (true, label == side.Parsed.Label ? side.Raw : side.Parsed.Format(label));
    }

    private sealed record Line(MemoryIndexLine Parsed, string Raw);

    // Equal sides, or the side that changed against the base; both changed differently is no pick.
    private static (bool Found, string? Value) Pick(string? b, string? o, string? t) =>
        o == t ? (true, o) : o == b ? (true, t) : t == b ? (true, o) : (false, null);

    private static string? Join(IReadOnlyList<string> lines) => lines.Count == 0 ? null : string.Join('\n', lines);

    private sealed record Index(IReadOnlyList<string> Header, IReadOnlyList<string> Order, IReadOnlyDictionary<string, Line> Entries);

    private static Index? Parse(string text)
    {
        var header = new List<string>();
        var order = new List<string>();
        var entries = new Dictionary<string, Line>(StringComparer.Ordinal);
        foreach (var raw in text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
        {
            if (MemoryIndexLine.TryParse(raw) is { } entry)
            {
                if (!entries.TryAdd(entry.Name, new Line(entry, raw))) return null;
                order.Add(entry.Name);
            }
            else if (order.Count == 0) header.Add(raw);
            else if (!string.IsNullOrWhiteSpace(raw)) return null;
        }
        if (text.Length == 0) header.Clear();
        return new Index(header, order, entries);
    }
}
