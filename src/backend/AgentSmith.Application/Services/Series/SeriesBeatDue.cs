using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: whether a series needs a beat now. The sweep reads the same facts a beat would and starts one when
/// the table moved since the last beat saw it — the first beat included, which has seen nothing.
/// 2026-11-25-9992e: a delivering series has no beat of its own at the CI poll any more — its master reads the final pull
/// requests' CI with ci "final" and merges them itself.
/// 2026-11-25-96aec: no beat runs on a cadence: when nothing changes, nothing is started. The events on its tickets make it
/// due only when one needs a decision (<see cref="Waking"/>) and the oldest has settled (<see cref="Woken"/>).
/// </summary>
public static class SeriesBeatDue
{
    public static string? Why(SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!record.IsActive) return null;
        var table = SeriesTable.Snapshot(SeriesTable.Rows(facts, now));
        if (table != record.LastTable) return "the table changed";
        if (record.Question is { Answer: null, DeadlineAt: { } deadline } && deadline <= now) return "a question's deadline passed"; // 0207c
        return record.Verdicts.LastOrDefault() is { Reported: false } ? "the series check reported" : null; // 0207d
    }

    public const string EventsArrived = "events arrived";

    /// <summary>2026-11-25-9992g: events wait for the master — unless the series is paused (it keeps them for the first beat
    /// after resume), the master's budget is spent (it could not judge them; the budget question waits instead), or its loop
    /// failed less than dialogue.series_status_minutes ago. 2026-11-25-96aec: and only events that need a decision, once the
    /// oldest of them is dialogue.series_wake_settle_seconds old, so a burst reaches the master as one turn.</summary>
    public static bool Woken(SeriesRunRecord record, DialogueGlobalConfig dialogue, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(dialogue);
        return record is { IsActive: true } && record.State != SeriesRunStates.Paused
            && record.MasterCostUsd < record.BudgetUsd(dialogue.SeriesMasterBudgetUsd) && Retry(record, now, dialogue.SeriesStatusMinutes)
            && record.Wakes.Where(w => w.At <= now && Waking(record, w)).Select(w => (DateTimeOffset?)w.At).Min() is { } oldest
            && now - oldest >= TimeSpan.FromSeconds(Math.Max(0, dialogue.SeriesWakeSettleSeconds));
    }

    /// <summary>2026-11-25-96aec: an event that needs the master's decision — not quiet, and not a change the master's own act
    /// recorded: a CI or pull-request event on a ticket recorded while an act of the master on its pull requests (merge, CI,
    /// refresh, retarget) ran — after it was called and before it returned. Such an act refreshes the ticket's records inside
    /// the call, before it is logged, and its answer told the master.</summary>
    public static bool Waking(SeriesRunRecord record, SeriesWake wake)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(wake);
        if (wake.Quiet || PersonMerges(record, wake)) return false;
        return wake.Kind is not (SeriesWakeKinds.Ci or SeriesWakeKinds.PullRequest)
            || !record.Acts.Any(a => a.Ticket == wake.TicketId && a is { StartedAt: { } from, DoneAt: { } until }
                && from <= wake.At && wake.At <= until && SeriesPullRequestTools.Reads(a.Tool));
    }

    /// <summary>2026-11-25-96aea: a series of one whose review lets a person decide the merge asks through its ticket's merge hold
    /// (its Merge button and pull request link): a green CI, or its turning next to merge, is no turn of the master's.</summary>
    public static bool PersonMerges(SeriesRunRecord record, SeriesWake? wake = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.IsOne && record.EffectiveReview != ReviewMode.None
            && (wake is null || wake.Kind == SeriesWakeKinds.Ci && wake.Verdict == Contracts.Reviews.CiStates.Green);
    }

    /// <summary>2026-11-25-9992g: the master's loop has not failed within dialogue.series_status_minutes.</summary>
    public static bool Retry(SeriesRunRecord record, DateTimeOffset now, int statusMinutes)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.MasterFailedAt is not { } failed || now - failed >= TimeSpan.FromMinutes(statusMinutes);
    }
}
