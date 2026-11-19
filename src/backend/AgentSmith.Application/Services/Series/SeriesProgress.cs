using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207d: where the series stands, moved on at the start of each beat. Once every ticket landed the series
/// check starts on the default branch; its verdict is reported once; a verdict that filed a follow-up keeps the series
/// running, one with no new failure ends it. A check that ended without a verdict is said and left to the master.
/// </summary>
public sealed class SeriesProgress(ISeriesRunStore store, ISeriesRunLaunch launch)
{
    public async Task MoveAsync(SeriesBeat beat, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(beat);
        if (beat.Record.Verdicts.LastOrDefault() is { Reported: false } verdict) await ReportAsync(beat, verdict, ct);
        else if (beat.Record.State == SeriesRunStates.Running && beat.Facts.Count > 0 && beat.Facts.All(f => f.Landed))
            await CheckAsync(beat, ct);
        else if (beat.Record is { State: SeriesRunStates.Verifying, VerifyRunId: { } run } && !Verdicted(beat.Record, run)
                 && await launch.FinishedAsync(run, ct))
            beat.Reasons.Add($"The series check (run {run}) ended without a verdict; check_series starts it again.");
    }

    /// <summary>Starts the series check; what the master's check_series and the last landing both do.</summary>
    public async Task<string> CheckAsync(SeriesBeat beat, CancellationToken ct)
    {
        if (beat.Record.State == SeriesRunStates.Paused) // 2026-11-19-1b72a: a pause holds the check as well
            return "The series is paused; the check starts again once the operator resumes it.";
        var run = await launch.LaunchAsync(beat.Record.Project, PipelinePresets.SeriesVerifyName, beat.Record.Id, ct);
        // 2026-11-19-1b72a: a pause pressed while the check launched is kept; the check resumes into Verifying.
        await KeepAsync(beat, r => r.State == SeriesRunStates.Paused
            ? r with { PausedFrom = SeriesRunStates.Verifying, VerifyRunId = run }
            : r with { State = SeriesRunStates.Verifying, VerifyRunId = run }, ct);
        beat.Said.Add($"All {beat.Facts.Count} tickets landed. The series check on main runs now (run {run}).");
        return $"The series check runs as {run}.";
    }

    private async Task ReportAsync(SeriesBeat beat, SeriesVerdict verdict, CancellationToken ct)
    {
        var followUp = verdict.FollowUpTicketId is { } id ? beat.Record.Tickets.FirstOrDefault(t => t.TicketId == id)?.Key ?? id : null;
        beat.Said.Add(SeriesSummaryText.Of(beat, verdict, followUp));
        // 2026-11-19-1b72a: a verdict that arrives during a pause ends the series it would have ended.
        var ends = followUp is null && (beat.Record.State == SeriesRunStates.Paused ? beat.Record.PausedFrom : beat.Record.State)
            == SeriesRunStates.Verifying;
        await KeepAsync(beat, r => r with
        {
            Verdicts = [.. r.Verdicts.Take(r.Verdicts.Count - 1), r.Verdicts[^1] with { Reported = true }], // not a limit: all but the last
            State = ends ? SeriesRunStates.Done : r.State, FinishedAt = ends ? beat.Now : r.FinishedAt,
            PausedFrom = ends ? null : r.PausedFrom,
        }, ct);
    }

    private static bool Verdicted(SeriesRunRecord record, string run) => record.Verdicts.Any(v => v.RunId == run);

    private async Task KeepAsync(SeriesBeat beat, Func<SeriesRunRecord, SeriesRunRecord> change, CancellationToken ct)
    {
        if (await store.UpdateAsync(beat.Record.Id, change, ct) is { } kept) beat.Record = kept;
    }
}
