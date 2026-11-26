namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-08-26-31e5: locates the <c>state:</c> / <c>done:</c> lines of a context.yaml and the
/// indentation its entries are written at, so the codec that edits them never has to guess
/// where the block is or how deep it sits.
/// </summary>
internal sealed record ContextStateBlock(int StateLine, int DoneLine, int DoneIndent, int EntryIndent)
{
    public const int Missing = -1;

    public static ContextStateBlock Locate(IReadOnlyList<string> lines)
    {
        var state = IndexOfKey(lines, "state", indent: 0, from: 0, until: lines.Count);
        if (state == Missing) return new ContextStateBlock(Missing, Missing, 2, 4);

        var end = EndOfBlock(lines, state);
        var doneIndent = IndentOfFirstKey(lines, state + 1, end, fallback: 2);
        var done = IndexOfKey(lines, "done", doneIndent, state + 1, end);
        if (done == Missing) return new ContextStateBlock(state, Missing, doneIndent, doneIndent + 2);

        var entryIndent = IndentOfFirstKey(lines, done + 1, EndOfBlock(lines, done), doneIndent + 2);
        return new ContextStateBlock(state, done, doneIndent, entryIndent);
    }

    /// <summary>The line after the last one belonging to the block opened at <paramref name="head"/>.</summary>
    public static int EndOfBlock(IReadOnlyList<string> lines, int head)
    {
        var indent = IndentOf(lines[head]);
        for (var i = head + 1; i < lines.Count; i++)
        {
            if (IsBlank(lines[i])) continue;
            if (IndentOf(lines[i]) <= indent) return i;
        }
        return lines.Count;
    }

    /// <summary>2026-11-26-462aa: <see cref="EndOfBlock"/> without the blank lines and the comments at or above the
    /// head's indent that close it — those introduce the next key, and removing the block must leave them.</summary>
    public static int EndOfOwnLines(IReadOnlyList<string> lines, int head)
    {
        var indent = IndentOf(lines[head]);
        var end = EndOfBlock(lines, head);
        while (end > head + 1 && IsBlank(lines[end - 1])
               && (lines[end - 1].Trim().Length == 0 || IndentOf(lines[end - 1]) <= indent))
            end--;
        return end;
    }

    /// <summary>The text after <c>key:</c> on its own line — empty when the value is a block.</summary>
    public static string InlineValue(string line)
    {
        var colon = line.IndexOf(':');
        return colon < 0 ? string.Empty : line[(colon + 1)..].Trim();
    }

    public static string KeyOf(string line)
    {
        var trimmed = line.TrimStart();
        var colon = trimmed.IndexOf(':');
        return colon < 0 ? string.Empty : trimmed[..colon].Trim().Trim('"', '\'');
    }

    public static int IndentOf(string line) => line.Length - line.TrimStart(' ').Length;

    public static bool IsBlank(string line) =>
        line.Trim().Length == 0 || line.TrimStart().StartsWith('#');

    private static int IndexOfKey(
        IReadOnlyList<string> lines, string key, int indent, int from, int until)
    {
        for (var i = from; i < until && i < lines.Count; i++)
        {
            if (IsBlank(lines[i])) continue;
            if (IndentOf(lines[i]) == indent && KeyOf(lines[i]) == key) return i;
        }
        return Missing;
    }

    private static int IndentOfFirstKey(
        IReadOnlyList<string> lines, int from, int until, int fallback)
    {
        for (var i = from; i < until && i < lines.Count; i++)
        {
            if (IsBlank(lines[i])) continue;
            return IndentOf(lines[i]);
        }
        return fallback;
    }
}
