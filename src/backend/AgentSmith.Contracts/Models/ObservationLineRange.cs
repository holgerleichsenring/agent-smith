namespace AgentSmith.Contracts.Models;

/// <summary>
/// Inclusive line span an observation targets, in new-file numbering — the
/// canonical anchor for PR-line-level findings. Skills emit it as a
/// <c>"start..end"</c> string (single line: <c>"42"</c>); the observation
/// parse path materializes it here. Absent on an observation means the
/// legacy <c>StartLine</c>/<c>EndLine</c> fields (or no line anchor at all).
/// </summary>
public sealed record ObservationLineRange(int Start, int End)
{
    /// <summary>
    /// Parses the canonical <c>"start..end"</c> form (also a bare
    /// <c>"42"</c> for a single line). Returns null for anything else;
    /// a reversed span is normalized so Start &lt;= End always holds.
    /// </summary>
    public static ObservationLineRange? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Split("..", StringSplitOptions.TrimEntries);
        if (parts.Length is not (1 or 2)) return null;
        if (!int.TryParse(parts[0], out var start) || start < 1) return null;
        if (parts.Length == 1) return new ObservationLineRange(start, start);
        if (!int.TryParse(parts[1], out var end) || end < 1) return null;
        return end < start
            ? new ObservationLineRange(end, start)
            : new ObservationLineRange(start, end);
    }

    public override string ToString() => Start == End ? $"{Start}" : $"{Start}..{End}";
}
