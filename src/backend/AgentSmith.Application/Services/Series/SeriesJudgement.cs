using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: why the series master is asked to judge a beat. A table that moved on by itself needs nobody: the
/// tickets start, merge and wake by their own runs. What needs judgement is a ticket that turned red since the last
/// beat — retry it, tell it what failed, or let it be. 2026-11-17-0207b: what the operator wrote. 2026-11-25-96aec: a
/// ticket that became the next merge; no reason comes from time alone.
/// </summary>
public static class SeriesJudgement
{
    public static IEnumerable<string> Operator(IReadOnlyList<SeriesOperatorMessage> messages) =>
        messages.Select(m => $"The operator wrote ({m.At.UtcDateTime:HH:mm}): {m.Text}");

    /// <summary>2026-11-25-9992g: each wake handed over, naming its ticket by key; a comment is quoted with its author like a
    /// message in the chat.</summary>
    public static IEnumerable<string> Woken(SeriesBeat beat, IEnumerable<SeriesWake> wakes)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(wakes);
        return wakes.Select(w => Woken(w, KeyOf(beat, w)));
    }

    /// <summary>2026-11-25-96aec: what was recorded meanwhile that needs no decision on its own, after the reasons.</summary>
    public static IEnumerable<string> Recorded(SeriesBeat beat, IEnumerable<SeriesWake> wakes)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(wakes);
        return wakes.Select(w => $"Recorded meanwhile, for your picture: {KeyOf(beat, w)} ({w.At.UtcDateTime:HH:mm}, {w.Kind}): {w.Text}");
    }

    private static string KeyOf(SeriesBeat beat, SeriesWake wake) =>
        beat.Facts.FirstOrDefault(f => f.Ticket.TicketId == wake.TicketId)?.Ticket.Key ?? wake.TicketId;

    private static string Woken(SeriesWake wake, string key) => wake.Kind == SeriesWakeKinds.Comment
        ? $"{wake.Author ?? "Someone"} commented on {key}{(wake.PrUrl is { } pr ? $"'s pull request {pr}" : string.Empty)} "
            + $"({wake.At.UtcDateTime:HH:mm}). {SeriesCommentText.Fenced(wake.Text)}"
        : $"{key} ({wake.At.UtcDateTime:HH:mm}, {wake.Kind}): {wake.Text}";

    /// <summary>2026-11-25-96aec: a ticket that became the series' next merge since the last beat — free, from the table, so a
    /// merge that unblocked it (the master's own included) brings the master back for it.</summary>
    public static IReadOnlyList<string> NewlyNext(SeriesRunRecord record, IReadOnlyList<SeriesTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(record);
        var before = SeriesTable.Parse(record.LastTable);
        return [.. rows.Where(r => r.Kind == SeriesTicketKinds.Now
                && (!before.TryGetValue(r.Ticket, out var was) || was.Kind is { } kind && kind != SeriesTicketKinds.Now))
            .Select(r => $"{r.Ticket} is next to merge: {r.State}")];
    }

    /// <summary>2026-11-26-5201f: a ticket held as the retarget whose words the last table did not show — only the master's
    /// retarget_pull_request moves it (the server's stacked retarget is gone), so it is a reason to judge.</summary>
    public static IReadOnlyList<string> NewlyRetarget(SeriesRunRecord record, SeriesBeat beat)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(beat);
        var before = SeriesTable.Parse(record.LastTable);
        return [.. beat.Rows.Zip(beat.Facts).Where(x => x.Second.Hold?.Case == Holds.TicketHoldCases.Retarget
                && (!before.TryGetValue(x.First.Ticket, out var was) || was.State != x.First.State))
            .Select(x => $"{x.First.Ticket} needs a retarget: {x.First.State}")];
    }

    /// <summary>2026-11-25-9992a: a failed ticket the last beat saw otherwise — absent, of another kind, or failed in other
    /// words. A line of a snapshot stored before kinds were kept names no kind and flags nothing: the words changed with the
    /// one state, not the tickets.</summary>
    public static IReadOnlyList<string> NewlyRed(SeriesRunRecord record, IReadOnlyList<SeriesTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(record);
        var before = SeriesTable.Parse(record.LastTable);
        return [.. rows.Where(r => r.Kind == SeriesTicketKinds.Failed && Turned(r, before))
            .Select(r => $"{r.Ticket} turned red: {r.State}")];
    }

    private static bool Turned(SeriesTableRow row, IReadOnlyDictionary<string, (string? Kind, string State)> before) =>
        !before.TryGetValue(row.Ticket, out var was) || (was.Kind is { } kind && (kind != row.Kind || was.State != row.State));
}
