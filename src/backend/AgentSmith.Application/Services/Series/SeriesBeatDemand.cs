using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: why a series needs a beat now, from its record and its tickets' facts; null when it
/// needs none. 2026-11-17-0207b: an operator message addressed to it is a reason.
/// 2026-11-25-9992e: a delivering series with final pull requests is read every merge.ci_poll_seconds, so a final pull request
/// its master merged or armed ends the series at the next poll. That reason only lets the delivery read; it is no master
/// reason (ReadSeriesHandler never turns it into one), so the model is not called for it.
/// 2026-11-25-96aec: a series check that ended without a verdict is a reason once per check run — its run carries no ticket,
/// so nothing else would tell the master.</summary>
public sealed class SeriesBeatDemand(ISeriesConversation conversation, AgentSmithConfig config, ISeriesRunLaunch launch)
{
    public const string FinalsRead = "the final pull requests are read";

    public const string CheckEnded = "the series check ended without a verdict";

    public async Task<string?> WhyAsync(
        SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        // 2026-11-25-96aec: while a failed master waits, nothing beats it — its reasons stay for the beat after the wait
        if (!SeriesBeatDue.Retry(record, now, config.Dialogue.SeriesStatusMinutes)) return null;
        if (SeriesBeatDue.Why(record, facts, now) is { } why) return why;
        if (!record.IsActive) return null;
        if (SeriesBeatDue.Woken(record, config.Dialogue, now)) return SeriesBeatDue.EventsArrived; // 2026-11-25-9992g
        if (FinalsDue(record, now)) return FinalsRead;
        if (await CheckEndedAsync(record, now, ct)) return CheckEnded;
        var waiting = await conversation.AddressedAsync(record.Conversation, record.Id, record.MessagesThrough, ct);
        return waiting.Count > 0 ? "the operator wrote" : null;
    }

    /// <summary>2026-11-25-96aec: the newest series check finished without a verdict and its master has not been told.</summary>
    public async Task<bool> CheckEndedAsync(SeriesRunRecord record, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record is { State: SeriesRunStates.Verifying, VerifyRunId: { } run } && !record.Raised.Contains(CheckKey(run))
            && record.Verdicts.All(v => v.RunId != run) && record.MasterCostUsd < record.BudgetUsd(config.Dialogue.SeriesMasterBudgetUsd)
            && await launch.FinishedAsync(run, ct);
    }

    /// <summary>The once-key of a check run whose end without a verdict was handed to the master.</summary>
    public static string CheckKey(string runId) => $"check:{runId}";

    // The last beat's clock: every beat that ran stamps LastStatusAt.
    private bool FinalsDue(SeriesRunRecord record, DateTimeOffset now) =>
        record is { State: SeriesRunStates.Delivering, Deliveries.Count: > 0 }
        && config.Projects.TryGetValue(record.Project, out var project)
        && (record.LastStatusAt is not { } last || now - last >= TimeSpan.FromSeconds(Math.Max(1, project.Merge.CiPollSeconds)));
}
