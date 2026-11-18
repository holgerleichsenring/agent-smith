using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: whether a series needs a beat now. The sweep reads the same facts a beat would and starts one when
/// the table moved since the last beat saw it — the first beat included, which has seen nothing.
/// </summary>
public static class SeriesBeatDue
{
    public static string? Why(SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!record.IsActive) return null;
        var table = SeriesTable.Snapshot(SeriesTable.Rows(facts, now));
        return table != record.LastTable ? "the table changed" : null;
    }
}
