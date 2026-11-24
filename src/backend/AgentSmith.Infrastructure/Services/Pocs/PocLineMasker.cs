using AgentSmith.Infrastructure.Services.Security;

namespace AgentSmith.Infrastructure.Services.Pocs;

/// <summary>
/// 2026-11-23-933dc: the snippet a PoC finding shows — its line with the UNION of every matched span on it replaced,
/// each by '&lt;secret at file:line&gt;' (from index and length, never from a shortened matched text), so one finding's
/// snippet never shows another's value; cut to a display window around the finding's own span.
/// </summary>
public sealed class PocLineMasker
{
    // A display window, so a minified one-line file never puts its whole line into VerdictJson or the prompt.
    private const int WindowChars = 80; // not a limit: a display window around the finding's own span
    private const string Cut = "…";

    public string Snippet(string line, IReadOnlyList<PatternLineMatch> all, PatternLineMatch own, string file, int lineNumber)
    {
        var mask = $"<secret at {file}:{lineNumber}>";
        var spans = Union(all);
        var masked = new System.Text.StringBuilder();
        var (ownStart, ownEnd, cursor) = (0, 0, 0);
        foreach (var (start, end) in spans)
        {
            masked.Append(line, cursor, start - cursor);
            if (own.Index >= start && own.Index < Math.Max(end, start + 1)) (ownStart, ownEnd) = (masked.Length, masked.Length + mask.Length);
            masked.Append(mask);
            cursor = end;
        }
        masked.Append(line, cursor, line.Length - cursor);
        return Window(masked.ToString(), ownStart, ownEnd);
    }

    private static List<(int Start, int End)> Union(IReadOnlyList<PatternLineMatch> matches)
    {
        var merged = new List<(int Start, int End)>();
        foreach (var (start, end) in matches.Select(m => (m.Index, m.Index + m.Length)).OrderBy(s => s.Item1))
            if (merged.Count > 0 && start <= merged[^1].End) merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, end));
            else merged.Add((start, end));
        return merged;
    }

    private static string Window(string masked, int start, int end)
    {
        var from = Math.Max(0, start - WindowChars);
        var to = Math.Min(masked.Length, end + WindowChars);
        return (from > 0 ? Cut : string.Empty) + masked[from..to] + (to < masked.Length ? Cut : string.Empty);
    }
}
