using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: whether a series needs a beat now. The sweep reads the same facts a beat would and starts one when
/// the table moved since the last beat saw it — the first beat included, which has seen nothing — and, from 0207b,
/// when the status cadence is due.
/// </summary>
public static class SeriesBeatDue
{
    public static string? Why(SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now, int statusMinutes)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!record.IsActive) return null;
        var table = SeriesTable.Snapshot(SeriesTable.Rows(facts, now));
        if (table != record.LastTable) return "the table changed";
        if (record.Question is { Answer: null, DeadlineAt: { } deadline } && deadline <= now) return "a question's deadline passed"; // 0207c
        if (record.Verdicts.LastOrDefault() is { Reported: false }) return "the series check reported"; // 0207d
        return Cadence(record, now, statusMinutes) ? "the status is due" : null;
    }

    /// <summary>2026-11-17-0207b: the series last spoke a whole status period ago.</summary>
    public static bool Cadence(SeriesRunRecord record, DateTimeOffset now, int statusMinutes) =>
        record.LastStatusAt is not { } last || now - last >= TimeSpan.FromMinutes(statusMinutes);
}
