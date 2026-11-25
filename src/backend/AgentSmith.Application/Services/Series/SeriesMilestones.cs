using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-433fa: the milestones a beat tells the chat, one sentence each and only when they happen — never a table, never
/// a "still running" line (2026-11-25-e94eb/e94ec). Both are told against the table the last beat saw, so the first beat, which
/// saw none, tells neither. A question, the final pull request's opening and the series' end are said where they happen.
/// </summary>
public static class SeriesMilestones
{
    /// <summary>The members that merged since the last beat, with how many are in — less the ones whose newest act is the
    /// master's own merge, which was said with that act (2026-11-25-96aeb: a merge it was refused says nothing of the kind).</summary>
    public static string? Merged(SeriesBeat beat)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var merged = Turned(beat, SeriesTicketKinds.Merged)
            .Where(f => !SeriesTicketMerge.Merged(beat.Record.Acts.LastOrDefault(a => a.Ticket == f.Ticket.TicketId)))
            .Select(f => f.Ticket.Key).ToList();
        return merged.Count == 0 ? null
            : $"{Join(merged)} merged; {beat.Facts.Count(f => f.Landed)} of {beat.Facts.Count} are in.";
    }

    /// <summary>The members that got stuck since the last beat — a failure a person decides — that the master neither acted on
    /// nor asked about in this beat; its own line or question says those.</summary>
    public static IReadOnlyList<string> Stuck(SeriesBeat beat)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var now = beat.Record.Acts.Where(a => a.At == beat.Now).ToList();
        // A question asked this beat — the master's, a relayed one, the budget's — is what the person reads instead.
        if (now.Any(a => a.Tool == SeriesAskTool.Name) || beat.Record.Question?.AskedAt == beat.Now) return [];
        return [.. Turned(beat, SeriesTicketKinds.Failed).Where(f => !now.Any(a => a.Ticket == f.Ticket.TicketId))
            .Select(f => $"{f.Ticket.Key} is stuck ({f.State}) and waits for your decision.")];
    }

    // A member the last table knew with another kind; a line stored before kinds were kept tells nothing.
    private static IEnumerable<SeriesTicketFact> Turned(SeriesBeat beat, string kind)
    {
        var before = SeriesTable.Parse(beat.Record.LastTable);
        return beat.Facts.Where(f => f.Kind == kind && before.TryGetValue(f.Ticket.Key, out var was) && was.Kind is { } had && had != kind);
    }

    private static string Join(IReadOnlyList<string> keys) =>
        keys.Count == 1 ? keys[0] : $"{string.Join(", ", keys.Take(keys.Count - 1))} and {keys[^1]}";
}
