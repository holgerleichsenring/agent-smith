using System.Text.RegularExpressions;

namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-09-17-042eb: a title every tracker accepts. The trackers cap a title near 255
/// characters and nothing bounded what the framework filed, so a long goal failed the create.
/// <para>
/// A title is one line, so line breaks become spaces. A cut falls at whitespace only when that
/// keeps most of the title — "p9000a:…" for a phase whose goal is one long url says nothing — and
/// otherwise is a hard cut that never splits a surrogate pair. The ellipsis says it cut; the
/// whole text belongs in the body.
/// </para>
/// </summary>
public static partial class TicketTitle
{
    public const int MaxLength = 255;

    private const string Cut = "…";

    public static string Fit(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var line = LineBreaks().Replace(title, " ");
        if (line.Length <= MaxLength) return line;
        var limit = MaxLength - Cut.Length;
        return line[..CutAt(line, limit)].TrimEnd() + Cut;
    }

    private static int CutAt(string line, int limit)
    {
        var earliestBoundary = limit * 6 / 10;
        for (var i = limit; i >= earliestBoundary; i--)
            if (char.IsWhiteSpace(line[i])) return i;
        return char.IsHighSurrogate(line[limit - 1]) ? limit - 1 : limit;
    }

    [GeneratedRegex(@"[\r\n]+")]
    private static partial Regex LineBreaks();
}
