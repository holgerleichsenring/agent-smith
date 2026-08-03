using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: cuts the ticket text into numbered, addressable blocks BEFORE the model
/// sees it. This is the anchor vocabulary the whole derivation rests on: the model
/// answers with segment ids, and code cuts the text back out byte for byte.
/// <para>
/// The rule is deliberately dumb so it is reproducible: blank lines separate
/// segments, and a fenced code block is one segment no matter how many blank lines
/// it contains — a migration manual's templates are exactly the content a
/// paragraph splitter would shred.
/// </para>
/// </summary>
public static class TicketSegmenter
{
    private const string Fence = "```";

    public static IReadOnlyList<TicketSegment> Segment(string? ticketText)
    {
        if (string.IsNullOrWhiteSpace(ticketText)) return [];

        var lines = ticketText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var segments = new List<TicketSegment>();
        var current = new List<string>();
        var startLine = 0;
        var inFence = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var isFence = line.TrimStart().StartsWith(Fence, StringComparison.Ordinal);

            if (!inFence && current.Count == 0 && line.Trim().Length == 0) continue;
            if (current.Count == 0) startLine = i + 1;

            if (isFence) inFence = !inFence;

            if (!inFence && !isFence && line.Trim().Length == 0)
            {
                Flush(segments, current, startLine, i);
                continue;
            }

            current.Add(line);
            // The closing fence ends the block: what follows is a new segment even
            // without a blank line between them.
            if (isFence && !inFence) Flush(segments, current, startLine, i + 1);
        }

        Flush(segments, current, startLine, lines.Length);
        return segments;
    }

    /// <summary>
    /// Renders the segmented ticket for the model — every block prefixed with the id
    /// it must answer with. The text itself is untouched: the model reads the manual,
    /// it just cannot address it by anything other than an id.
    /// </summary>
    public static string Render(IReadOnlyList<TicketSegment> segments)
    {
        var sb = new StringBuilder();
        foreach (var segment in segments)
        {
            sb.Append("[[").Append(segment.Id).Append("]]\n");
            sb.Append(segment.Text).Append("\n\n");
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    private static void Flush(List<TicketSegment> segments, List<string> current, int startLine, int endLine)
    {
        if (current.Count == 0) return;
        segments.Add(new TicketSegment(
            segments.Count + 1, string.Join('\n', current), startLine, endLine));
        current.Clear();
    }
}
