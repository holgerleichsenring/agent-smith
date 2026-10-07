using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-07-6b9da: bounds what ONE tool result may add to a conversation, in every tool loop.
/// <para>
/// The cut keeps the HEAD and the TAIL (p0422; a head-only cut lost a build verdict, p0419) and
/// the whole output, marker included, fits the budget. The marker names the dropped and the TRUE
/// total characters, says the unshown part may still hold what the model looks for — a cut list
/// is not an inventory (485e) — and how to page for the rest.
/// </para>
/// <para>
/// A string returned through AIFunctionFactory arrives as a JSON string; it comes out as plain
/// text, because a provider JSON-serializes anything that is not a string and the escaping
/// roughly doubled a lockfile on the wire. A result within budget comes back with the same text,
/// so a second pass over a bounded result changes nothing.
/// </para>
/// </summary>
public static partial class ToolResultBound
{
    public const int DefaultBudget = 100_000;

    /// <summary>
    /// Bounds a string or JSON-string result to <paramref name="budget"/> characters; any other
    /// result passes through unchanged. <paramref name="knownTotal"/> is the size of the output a
    /// caller holds only part of; the marker states it when it exceeds the text.
    /// </summary>
    public static object? Apply(object? result, int budget = DefaultBudget, long? knownTotal = null)
    {
        var text = TextOf(result);
        if (text is null || text.Length <= budget) return text ?? result;
        var middle = text.Length / 2;
        return Cut(text[..middle], text[middle..], budget, Math.Max(knownTotal ?? 0, text.Length));
    }

    /// <summary>
    /// 2026-10-07-6b9db: bounds an output held as two separate pieces — its first characters and
    /// its last ones — of <paramref name="total"/> characters in all. Whatever lies between the
    /// pieces was never held, so the cut always falls there and the kept tail never runs across
    /// the gap. Pieces that hold the whole output within budget come back joined and unchanged.
    /// </summary>
    public static string ApplyParts(string head, string tail, int budget, long total)
    {
        long held = head.Length + tail.Length;
        if (held <= budget && total <= held) return head + tail;
        return Cut(head, tail, budget, Math.Max(total, held));
    }

    /// <summary>
    /// Shrinks an already bounded text to a smaller <paramref name="budget"/>, keeping the total
    /// its marker states — this bound's own, or BoundedResultTool's head-only one. Unmarked text
    /// is bounded as <see cref="Apply"/> would.
    /// </summary>
    public static string Recut(string text, int budget)
    {
        if (text.Length <= budget) return text;
        var own = OwnMarker().Match(text);
        if (own.Success)
            return Cut(text[..own.Index], text[(own.Index + own.Length)..], budget, Count(own));
        var headOnly = HeadOnlyMarker().Match(text);
        if (headOnly.Success)
            return Cut(text[..headOnly.Index], string.Empty, budget, Count(headOnly));
        return (string)Apply(text, budget)!;
    }

    /// <summary>The text of a string or JSON-string tool result; null for any other result.</summary>
    public static string? TextOf(object? result) => result switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
        _ => null,
    };

    // Sized for the widest marker (dropped == total), so the real one — dropped is never wider —
    // leaves the output within budget. A budget smaller than the marker itself keeps the marker.
    private static string Cut(string head, string tail, int budget, long total)
    {
        var room = Math.Max(0, budget - Marker(total, total, tail.Length > 0).Length);
        var tailKept = Math.Min(tail.Length, room / 2);
        var headKept = Math.Min(head.Length, room - tailKept);
        tailKept = Math.Min(tail.Length, room - headKept);
        var marker = Marker(total - headKept - tailKept, total, tailKept > 0);
        return head[..headKept] + marker + tail[^tailKept..];
    }

    private static string Marker(long dropped, long total, bool tailShown) =>
        $"\n\n[… {Number(dropped)} of {Number(total)} characters cut from the {(tailShown ? "middle" : "end")} "
            + $"of this result — only its {(tailShown ? "head (above) and tail (below) are" : "head (above) is")} "
            + "shown. What is not shown may still exist: this is not a complete listing. To read more, "
            + "page with read_file start_line/line_count, or ask again with a narrower path or pattern.]\n\n";

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static long Count(Match marker) =>
        long.Parse(marker.Groups["total"].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\n\n\[… [\d,]+ of (?<total>[\d,]+) characters cut from the (?:middle|end) of this result — [^\]]*\]\n\n")]
    private static partial Regex OwnMarker();

    // BoundedResultTool.Bound's head-only marker: "[truncated: X of Y characters omitted …]".
    [GeneratedRegex(@"\n… \[truncated: \d+ of (?<total>\d+) characters omitted[^\]]*\]")]
    private static partial Regex HeadOnlyMarker();
}
