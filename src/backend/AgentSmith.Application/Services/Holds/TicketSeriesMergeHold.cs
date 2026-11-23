using AgentSmith.Application.Services.Series;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-849e: the holds a series' merge order gives. ONE ticket per series is a person's merge — the step ready now,
/// naming the step after it and the series' tickets that start only once others merge; a never-run ticket that starts on
/// merges is nobody's: it waits for that many merges, the series' next one first, and its act opens the series.
/// </summary>
public static class TicketSeriesMergeHold
{
    /// <summary>The ticket's pull requests merge next in its series: the person's one merge there.</summary>
    public static TicketHold? Next(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.MergeStep is not { Wait: SeriesRunMergeWaits.Ready } step || facts.SeriesId is not { } series) return null;
        var after = facts.SeriesMergeOrder.SkipWhile(s => s.TicketId != step.TicketId).Skip(1).FirstOrDefault();
        return TicketHoldOf.Hold(facts, TicketHoldAddressees.Merge, TicketHoldCases.MergeNext,
                new(TicketHoldActs.Merge, series), facts.EndedAt)
            with { Names = facts.SeriesStartsOnMerge, Next = after?.Key, Approves = facts.MergeApproves };
    }

    /// <summary>A never-run ticket that starts once others merge: it waits on nobody for the merges up to the last one it
    /// needs in the merge order (one more for each it needs that has no pull request open yet). The next merge is the
    /// series' ready one when that comes before what it needs, else the first it needs in the order, else the first it
    /// needs at all.</summary>
    public static TicketHold? StartsOnMerge(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.StartsOnceMerged.Count == 0) return null;
        var order = facts.SeriesMergeOrder.Select(s => s.Key).ToList();
        var places = facts.StartsOnceMerged.Select(k => order.IndexOf(k)).Where(p => p >= 0).ToList();
        var merges = (places.DefaultIfEmpty(-1).Max() + 1) + (facts.StartsOnceMerged.Count - places.Count);
        var ready = facts.SeriesMergeOrder.ToList().FindIndex(s => s.Wait == SeriesRunMergeWaits.Ready);
        var next = places.Count == 0 ? facts.StartsOnceMerged[0]
            : ready >= 0 && ready <= places.Max() ? order[ready] : order[places.Min()];
        return TicketHoldOf.Hold(facts, TicketHoldAddressees.Nobody, TicketHoldCases.StartsOnMerge,
                facts.SeriesId is { } series ? new(TicketHoldActs.Series, series) : null, facts.FiledAt)
            with { Names = facts.StartsOnceMerged, Merges = merges, Next = next };
    }
}
