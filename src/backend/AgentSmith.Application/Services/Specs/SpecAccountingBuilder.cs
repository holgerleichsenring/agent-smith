using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: turns the derivation's claims into the accounting a reviewer checks in
/// seconds — and finds the segments nobody spoke for.
/// <para>
/// The accounting spans the UNION of the specs: a segment is carried if any spec
/// carries it. It is a LIST, not a percentage: a percentage gets optimised against
/// the moment it is measured.
/// </para>
/// </summary>
public static class SpecAccountingBuilder
{
    public static SpecAccounting Build(
        IReadOnlyList<DerivedSpec> specs,
        IReadOnlyList<DiscardedSegment> discarded,
        IReadOnlyList<TicketSegment> segments,
        IReadOnlyList<DiscardedContext>? discardedContexts = null,
        IReadOnlyList<int>? shared = null)
    {
        var carried = specs
            .SelectMany(p => p.CarriedSegments.Select(id => new CarriedSegment(id, p.SpecId)))
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
            discardedContexts)
        {
            Shared = [.. (shared ?? []).Where(id => segments.Any(s => s.Id == id)).Distinct().Order()],
        };
    }

    /// <summary>The discarded list, short enough to sit in a pull-request body.</summary>
    public static string RenderDiscardedForPullRequest(SpecAccounting accounting)
    {
        if (accounting.Discarded.Count == 0)
            return "_Nothing in the ticket was discarded._";
        return string.Join("\n", accounting.Discarded
            .Select(d => $"- segment {d.SegmentId}: {d.Reason}"));
    }

    /// <summary>2026-10-17-293ba: the segments every spec carries, under their own heading so a
    /// reader never takes them for discarded ones; empty when the cut declared none.</summary>
    public static string RenderSharedForPullRequest(SpecAccounting accounting) =>
        accounting.Shared.Count == 0
            ? string.Empty
            : "## Carried by every spec\n\nsegment(s) " + string.Join(", ", accounting.Shared) + "\n";

    /// <summary>2026-09-08-1830: the named contexts the cut left out, with their reasons.</summary>
    public static string RenderDiscardedContexts(SpecAccounting accounting) =>
        string.Join("\n", accounting.DiscardedContexts.Select(d => $"- {d.Context}: {d.Reason}"));
}
