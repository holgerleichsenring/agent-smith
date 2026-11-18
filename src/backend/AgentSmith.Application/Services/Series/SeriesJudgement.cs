using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: why the series master is asked to judge a beat. A table that moved on by itself needs nobody: the
/// tickets start, merge and wake by their own runs. What needs judgement is a ticket that turned red since the last
/// posted table — retry it, tell it what failed, or let it be. 2026-11-17-0207b: what the operator wrote, and a
/// series that stood still for a whole status period while tickets run.
/// </summary>
public static class SeriesJudgement
{
    public static IEnumerable<string> Operator(IReadOnlyList<SeriesOperatorMessage> messages) =>
        messages.Select(m => $"The operator wrote ({m.At.UtcDateTime:HH:mm}): {m.Text}");

    /// <summary>Nothing moved since the last status a whole period ago while tickets run: is one stuck?</summary>
    public static IEnumerable<string> Stalled(SeriesBeat beat, int statusMinutes)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var running = beat.Rows.Where(r => r.Kind == SeriesTicketState.Running).Select(r => r.Ticket).ToList();
        if (running.Count == 0 || beat.Table != beat.Record.LastTable
            || !SeriesBeatDue.Cadence(beat.Record, beat.Now, statusMinutes)) return [];
        return [$"Nothing moved for {statusMinutes} minutes while {string.Join(", ", running)} run(s): is one slow or stuck?"];
    }

    public static IReadOnlyList<string> NewlyRed(SeriesRunRecord record, IReadOnlyList<SeriesTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(record);
        var before = SeriesTable.Parse(record.LastTable);
        return [.. rows.Where(r => r.Kind == SeriesTicketState.Red
                && (!before.TryGetValue(r.Ticket, out var was) || was != r.State))
            .Select(r => $"{r.Ticket} turned red: {r.State}")];
    }
}
