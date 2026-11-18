namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-17-3435a: one failure's excerpt — what the host summarised (title, summary, annotations, error issues), at most
/// half of the bound, then the end of its log in the rest, within the project's merge.ci_excerpt_chars. The log's END is kept: a build or test failure is
/// said in its last lines, and the head of a CI log is checkout and setup.
/// </summary>
public static class CiExcerpt
{
    public const string Cut = "[… earlier output cut at merge.ci_excerpt_chars]\n";

    public static string Of(IEnumerable<string?> summary, string? log, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var max = Math.Max(0, maxChars);
        var head = string.Join("\n", summary.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        if (string.IsNullOrWhiteSpace(log)) return head.Length <= max ? head : head[..max];
        // The host's own words keep at most half; the log's end takes the rest.
        var kept = head.Length <= max / 2 ? head : head[..(max / 2)];
        var joint = kept.Length > 0 ? $"{kept}\n{LogMark}" : string.Empty;
        return joint + Tail(log.TrimEnd(), Math.Max(0, max - joint.Length));
    }

    public const string LogMark = "--- log tail ---\n";

    /// <summary>The last <paramref name="maxChars"/> characters, marked as cut when anything was dropped.</summary>
    public static string Tail(string text, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= maxChars) return text;
        var keep = Math.Max(0, maxChars - Cut.Length);
        return Cut + text[^keep..];
    }
}
