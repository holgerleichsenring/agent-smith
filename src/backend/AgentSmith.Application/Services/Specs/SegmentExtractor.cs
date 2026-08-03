using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: cuts the anchored segments out of the ticket byte-exact and writes one
/// phase's markdown companion. Deterministic and independent of the loaded pin:
/// judgement is external, extraction is not. The model never reproduces a template,
/// because text a model retypes is text that drifts — and a plausible copy of a
/// naming contract is the failure mode this line of work started from.
/// </summary>
public static class SegmentExtractor
{
    public static string BuildMarkdown(
        string phaseId, string goal,
        IReadOnlyList<int> carriedSegmentIds,
        IReadOnlyList<TicketSegment> segments)
    {
        var byId = segments.ToDictionary(s => s.Id);
        var sb = new StringBuilder();
        sb.Append("# ").Append(phaseId).Append(" — verbatim from the ticket\n\n");
        sb.Append(goal).Append("\n\n");
        sb.Append(
            "Every block below is copied byte for byte out of the ticket. "
            + "Nothing here is a summary; the segment id is the ticket's own anchor.\n");

        foreach (var id in carriedSegmentIds.Distinct().OrderBy(id => id))
        {
            if (!byId.TryGetValue(id, out var segment)) continue;
            sb.Append("\n## segment ").Append(id)
                .Append(" (ticket lines ").Append(segment.FirstLine)
                .Append('–').Append(segment.LastLine).Append(")\n\n");
            sb.Append(segment.Text.TrimEnd()).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// The whole ticket as one companion — the fail-safe shape. Used when the
    /// accounting cannot be produced: degrading to too much context is cheap,
    /// degrading to a missing manual nobody noticed is the failure the accounting
    /// exists to prevent.
    /// </summary>
    public static string BuildWholeTicketMarkdown(
        string phaseId, string goal, IReadOnlyList<TicketSegment> segments) =>
        BuildMarkdown(phaseId, goal, [.. segments.Select(s => s.Id)], segments);
}
