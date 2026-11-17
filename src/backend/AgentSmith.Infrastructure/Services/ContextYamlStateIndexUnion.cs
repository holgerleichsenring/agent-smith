using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-11-17-023fb: the union of two context.yaml files whose only real difference is state.done entries each side
/// added, read with the same <see cref="ContextStateBlock"/> the codec writes with, so the two cannot disagree about
/// where the block is. Outside state.done the sides must be equal, or one of them unchanged from the base; the result
/// is the changed side with the other side's added entries inserted under done, newest first as the codec writes.
/// </summary>
public sealed class ContextYamlStateIndexUnion : IContextYamlStateIndexUnion
{
    public string? Resolve(string? @base, string? ours, string? theirs)
    {
        if (ours is null || theirs is null) return null;
        var (b, o, t) = (Split(@base ?? string.Empty), Split(ours), Split(theirs));
        if (b.Entries.Any(e => !Same(o, e.Key, e.Value) || !Same(t, e.Key, e.Value))) return null;
        var addedOurs = o.Order.Where(k => !b.Entries.ContainsKey(k)).ToList();
        var addedTheirs = t.Order.Where(k => !b.Entries.ContainsKey(k)).ToList();
        if (addedOurs.Intersect(addedTheirs).Any(k => o.Entries[k] != t.Entries[k])) return null;

        var (chosen, other, otherAdded) = o.Skeleton == t.Skeleton || t.Skeleton == b.Skeleton ? (o, t, addedTheirs)
            : o.Skeleton == b.Skeleton ? (t, o, addedOurs) : default;
        if (chosen is null) return null;
        var missing = otherAdded.Where(k => !chosen.Entries.ContainsKey(k)).ToList();
        return missing.Count == 0 ? chosen.Text : Insert(chosen, missing.Select(k => other.Entries[k]));
    }

    private static bool Same(Parsed side, string key, string text) =>
        side.Entries.TryGetValue(key, out var mine) && mine == text;

    private static string? Insert(Parsed side, IEnumerable<string> entries)
    {
        var lines = side.Text.Split('\n').ToList();
        var block = ContextStateBlock.Locate(lines);
        if (block.DoneLine == ContextStateBlock.Missing) return null;
        var inline = ContextStateBlock.InlineValue(lines[block.DoneLine]);
        if (inline.Length > 0 && inline != "{}") return null;
        if (inline == "{}") lines[block.DoneLine] = new string(' ', block.DoneIndent) + "done:";
        lines.InsertRange(block.DoneLine + 1, entries.SelectMany(e => e.Split('\n')));
        return string.Join('\n', lines);
    }

    /// <summary>A file cut into its state.done entries (key to the entry's lines) and everything else.</summary>
    private static Parsed Split(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        var block = ContextStateBlock.Locate(lines);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = new List<string>();
        var skeleton = new List<string>(lines);
        if (block.DoneLine != ContextStateBlock.Missing)
        {
            var end = ContextStateBlock.EndOfBlock(lines, block.DoneLine);
            for (var i = block.DoneLine + 1; i < end; i++)
            {
                if (ContextStateBlock.IsBlank(lines[i]) || ContextStateBlock.IndentOf(lines[i]) != block.EntryIndent) continue;
                var tail = Math.Min(ContextStateBlock.EndOfBlock(lines, i), end);
                var key = ContextStateBlock.KeyOf(lines[i]);
                entries[key] = string.Join('\n', lines.GetRange(i, tail - i));
                order.Add(key);
            }
            skeleton = [.. lines.Take(block.DoneLine), new string(' ', block.DoneIndent) + "done:", .. lines.Skip(end)];
        }
        return new Parsed(text, entries, order, string.Join('\n', skeleton).TrimEnd());
    }

    private sealed record Parsed(string Text, Dictionary<string, string> Entries, List<string> Order, string Skeleton);
}
