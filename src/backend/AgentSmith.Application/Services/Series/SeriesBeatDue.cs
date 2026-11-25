using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: whether a series needs a beat now. The sweep reads the same facts a beat would and starts one when
/// the table moved since the last beat saw it — the first beat included, which has seen nothing — and, from 0207b,
/// when the status cadence is due. 2026-11-25-e94eb: no status is posted any more; the cadence is the stall check's —
/// a beat a whole dialogue.series_status_minutes after the last, which asks the master when nothing moved.
/// 2026-11-25-9992e: a delivering series has no beat of its own at the CI poll any more — its master reads the final pull
/// requests' CI with ci "final" and merges them itself.
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
        return Cadence(record, now, statusMinutes) ? "the stall check is due" : null;
    }

    public const string EventsArrived = "events arrived";

    /// <summary>2026-11-25-9992g: events wait for the master — unless the series is paused (it keeps them for the first beat
    /// after resume), the master's budget is spent (it could not judge them; the budget question waits instead), or its loop
    /// failed less than a status period ago (the next try waits for the stall cadence, not the next sweep).</summary>
    public static bool Woken(SeriesRunRecord record, decimal configuredBudgetUsd, DateTimeOffset now, int statusMinutes)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record is { IsActive: true, Wakes.Count: > 0 } && record.State != SeriesRunStates.Paused
            && record.MasterCostUsd < record.BudgetUsd(configuredBudgetUsd)
            && (record.MasterFailedAt is not { } failed || now - failed >= TimeSpan.FromMinutes(statusMinutes));
    }

    /// <summary>2026-11-17-0207b: the series' last beat ran a whole status period ago.</summary>
    public static bool Cadence(SeriesRunRecord record, DateTimeOffset now, int statusMinutes) =>
        record.LastStatusAt is not { } last || now - last >= TimeSpan.FromMinutes(statusMinutes);
}
