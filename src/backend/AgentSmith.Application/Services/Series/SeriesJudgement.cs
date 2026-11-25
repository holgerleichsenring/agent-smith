using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: why the series master is asked to judge a beat. A table that moved on by itself needs nobody: the
/// tickets start, merge and wake by their own runs. What needs judgement is a ticket that turned red since the last
/// beat — retry it, tell it what failed, or let it be. 2026-11-17-0207b: what the operator wrote, and a
/// series that stood still for a whole status period while tickets run.
/// </summary>
public static class SeriesJudgement
{
    public static IEnumerable<string> Operator(IReadOnlyList<SeriesOperatorMessage> messages) =>
        messages.Select(m => $"The operator wrote ({m.At.UtcDateTime:HH:mm}): {m.Text}");

    /// <summary>2026-11-25-9992g: each wake the beat hands over, naming its ticket by key; a comment is quoted with its
    /// author like a message in the chat.</summary>
    public static IEnumerable<string> Woken(SeriesBeat beat)
    {
        ArgumentNullException.ThrowIfNull(beat);
        return beat.Wakes.Select(w => Woken(w, beat.Facts.FirstOrDefault(f => f.Ticket.TicketId == w.TicketId)?.Ticket.Key ?? w.TicketId));
    }

    private static string Woken(SeriesWake wake, string key) => wake.Kind == SeriesWakeKinds.Comment
        ? $"{wake.Author ?? "Someone"} commented on {key}{(wake.PrUrl is { } pr ? $"'s pull request {pr}" : string.Empty)} "
            + $"({wake.At.UtcDateTime:HH:mm}). {SeriesCommentText.Fenced(wake.Text)}"
        : $"{key} ({wake.At.UtcDateTime:HH:mm}, {wake.Kind}): {wake.Text}";

    /// <summary>Nothing moved since the last beat a whole period ago while tickets run: is one stuck?</summary>
    public static IEnumerable<string> Stalled(SeriesBeat beat, int statusMinutes)
    {
        ArgumentNullException.ThrowIfNull(beat);
        // 2026-11-22-d434b: a run waiting for its model place is another run's turn, not a stall
        var running = beat.Facts.Where(f => f.Working && f.Hold?.Case != Holds.TicketHoldCases.ModelWait).Select(f => f.Ticket.Key).ToList();
        if (running.Count == 0 || beat.Table != beat.Record.LastTable
            || !SeriesBeatDue.Cadence(beat.Record, beat.Now, statusMinutes)) return [];
        return [$"Nothing moved for {statusMinutes} minutes while {string.Join(", ", running)} run(s): is one slow or stuck?"];
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
