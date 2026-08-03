using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: turns the derivation's claims into the accounting a reviewer checks in
/// seconds — and finds the segments nobody spoke for.
/// <para>
/// The accounting spans the UNION of the phases: a segment is carried if any phase
/// carries it. It is a LIST, not a percentage: a percentage gets optimised against
/// the moment it is measured.
/// </para>
/// </summary>
public static class SpecAccountingBuilder
{
    public static SpecAccounting Build(
        IReadOnlyList<SpecPhase> phases,
        IReadOnlyList<DiscardedSegment> discarded,
        IReadOnlyList<TicketSegment> segments)
    {
        var carried = phases
            .SelectMany(p => p.CarriedSegments.Select(id => new CarriedSegment(id, p.PhaseId)))
            .Where(c => segments.Any(s => s.Id == c.SegmentId))
            .OrderBy(c => c.SegmentId)
            .ToList();
        var accountedFor = carried.Select(c => c.SegmentId)
            .Concat(discarded.Where(d => d.Reason.Length > 0).Select(d => d.SegmentId))
            .ToHashSet();
        var unaccounted = segments
            .Select(s => s.Id)
            .Where(id => !accountedFor.Contains(id))
            .ToList();

        return new SpecAccounting(
            carried,
            [.. discarded.Where(d => d.Reason.Length > 0 && segments.Any(s => s.Id == d.SegmentId))
                .OrderBy(d => d.SegmentId)],
            unaccounted);
    }

    /// <summary>
    /// The accounting as the reviewer reads it in the pull request. Discarded first:
    /// what was left out is the part a human has to agree with.
    /// </summary>
    public static string Render(
        SpecAccounting accounting, IReadOnlyList<TicketSegment> segments, string ticketId)
    {
        var sb = new StringBuilder();
        sb.Append("# Ticket ").Append(ticketId).Append(" — what was carried, what was not\n\n");
        sb.Append("Every block of the ticket is either carried by a phase or discarded with a "
            + "reason. Nothing is summarised away silently.\n\n");

        sb.Append("## Discarded\n\n");
        if (accounting.Discarded.Count == 0)
            sb.Append("_Nothing was discarded._\n");
        foreach (var d in accounting.Discarded)
            sb.Append("- segment ").Append(d.SegmentId).Append(": ").Append(d.Reason)
                .Append("\n  > ").Append(FirstLine(segments, d.SegmentId)).Append('\n');

        sb.Append("\n## Carried\n\n");
        foreach (var group in accounting.Carried.GroupBy(c => c.PhaseId))
            sb.Append("- **").Append(group.Key).Append("**: segment(s) ")
                .Append(string.Join(", ", group.Select(c => c.SegmentId))).Append('\n');

        if (!accounting.IsComplete)
            sb.Append("\n## Unaccounted\n\nsegment(s) ")
                .Append(string.Join(", ", accounting.Unaccounted))
                .Append(" — the split was refused for this reason and the whole ticket is "
                    + "carried by a single phase instead.\n");

        return sb.ToString();
    }

    /// <summary>The discarded list, short enough to sit in a pull-request body.</summary>
    public static string RenderDiscardedForPullRequest(SpecAccounting accounting)
    {
        if (accounting.Discarded.Count == 0)
            return "_Nothing in the ticket was discarded._";
        return string.Join("\n", accounting.Discarded
            .Select(d => $"- segment {d.SegmentId}: {d.Reason}"));
    }

    private static string FirstLine(IReadOnlyList<TicketSegment> segments, int id)
    {
        var text = segments.FirstOrDefault(s => s.Id == id)?.Text ?? string.Empty;
        var line = text.Split('\n')[0].Trim();
        return line.Length <= 120 ? line : line[..120] + "…";
    }
}
