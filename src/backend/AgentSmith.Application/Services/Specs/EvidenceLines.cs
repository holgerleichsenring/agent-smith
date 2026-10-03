using System.Globalization;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: the text after a path's ':' read as line ranges, or null when it is not a
/// line list — including a number too large for an int, which is a malformed citation and not a
/// reason to throw.
/// </summary>
internal static class EvidenceLines
{
    public static IReadOnlyList<EvidenceLineRange>? Parse(string text)
    {
        if (!EvidenceGrammar.LineList().IsMatch(text)) return null;
        var ranges = new List<EvidenceLineRange>();
        foreach (var part in text.Split(','))
        {
            var bounds = part.Split('-');
            if (!TryInt(bounds[0], out var from)) return null;
            var to = from;
            if (bounds.Length > 1 && !TryInt(bounds[1], out to)) return null;
            ranges.Add(new EvidenceLineRange(from, to));
        }
        return ranges;
    }

    private static bool TryInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
