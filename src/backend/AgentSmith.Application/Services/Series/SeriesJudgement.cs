using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: why the series master is asked to judge a beat. A table that moved on by itself needs nobody: the
/// tickets start, merge and wake by their own runs. What needs judgement is a ticket that turned red since the last
/// posted table — retry it, tell it what failed, or let it be.
/// </summary>
public static class SeriesJudgement
{
    public static IReadOnlyList<string> NewlyRed(SeriesRunRecord record, IReadOnlyList<SeriesTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(record);
        var before = SeriesTable.Parse(record.LastTable);
        return [.. rows.Where(r => r.Kind == SeriesTicketState.Red
                && (!before.TryGetValue(r.Ticket, out var was) || was != r.State))
            .Select(r => $"{r.Ticket} turned red: {r.State}")];
    }
}
