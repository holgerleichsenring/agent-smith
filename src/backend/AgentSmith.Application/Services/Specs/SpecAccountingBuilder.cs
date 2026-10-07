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
        IReadOnlyList<TicketSegment> segments,
        IReadOnlyList<DiscardedContext>? discardedContexts = null)
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
            unaccounted,
            discardedContexts);
    }

    /// <summary>The discarded list, short enough to sit in a pull-request body.</summary>
    public static string RenderDiscardedForPullRequest(SpecAccounting accounting)
    {
        if (accounting.Discarded.Count == 0)
            return "_Nothing in the ticket was discarded._";
        return string.Join("\n", accounting.Discarded
            .Select(d => $"- segment {d.SegmentId}: {d.Reason}"));
    }

    /// <summary>2026-09-08-1830: the named contexts the cut left out, with their reasons.</summary>
    public static string RenderDiscardedContexts(SpecAccounting accounting) =>
        string.Join("\n", accounting.DiscardedContexts.Select(d => $"- {d.Context}: {d.Reason}"));
}
