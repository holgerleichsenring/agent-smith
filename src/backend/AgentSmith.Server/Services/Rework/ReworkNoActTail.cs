using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-8762v: the end of one nudge, lifted out of <see cref="ReworkNudgeHandler"/>. An unfinished attempt is
/// waited for; then the newest act is read. An act an operator's stop withholds is said once; a pending act is served
/// by the rework entry. With no act — none, or one already followed up — the ticket's series may go on by itself.
/// <para>2026-11-25-9992g: a series ticket's run end wakes its master, and a comment on the ticket or its pull request is
/// the master's to answer (it was woken by the comment), so it starts no rework here.</para>
/// </summary>
public sealed class ReworkNoActTail(
    IReworkPendingActs pending, ClosedTicketActFollowUp followUps, ReworkWithheld withheld, ISeriesAutoStart series,
    ISeriesRunStore seriesRuns, ISeriesWakes wakes, IRunReportReader? reports = null) // 2026-11-26-a12ed: the wake names its report
{
    public async Task<ReworkTail> ReadAsync(
        ResolvedProject project, ClaimedReworkNudge nudge, PreviousAttempt attempt, CancellationToken ct)
    {
        if (!attempt.Finished) return new(ReworkNudgeDisposition.Reschedule);
        var act = await pending.NewestAsync(project, nudge.TicketId, attempt, ct);
        if (await HeldAsync(project, nudge, attempt, ct) && act?.Act.Channel is ReworkChannel.Ticket or ReworkChannel.PullRequest)
            act = null;
        if (await SpentAsync(project, nudge, act, ct)) return new(await series.ContinueAsync(project, nudge.TicketId, ct));
        if (await withheld.SaysAsync(project, nudge.TicketId, act!, attempt, ct)) return new(ReworkNudgeDisposition.Finish);
        return new(ReworkNudgeDisposition.Finish, act);
    }

    /// <summary>No act, or one already followed up on a new ticket.</summary>
    public async Task<bool> SpentAsync(ResolvedProject project, ClaimedReworkNudge nudge, PendingReworkAct? act, CancellationToken ct) =>
        act is null || await followUps.IsFollowedUpAsync(project, nudge.TicketId, act, ct);

    // A run's end is recorded for the series holding the ticket; 2026-11-25-96aea: a comment act stays the ticket's own unless a
    // filed series leads it — a series of one keeps its ticket's rework.
    private async Task<bool> HeldAsync(ResolvedProject project, ClaimedReworkNudge nudge, PreviousAttempt attempt, CancellationToken ct)
    {
        if (nudge.Origin == ReworkNudgeOrigin.RunEnd) await RecordEndAsync(project, nudge, attempt, ct);
        return await seriesRuns.LeadsCommentsAsync(project.Name, nudge.TicketId, ct);
    }

    private async Task<bool> RecordEndAsync(ResolvedProject project, ClaimedReworkNudge nudge, PreviousAttempt attempt, CancellationToken ct) =>
        await wakes.WakeAsync(project.Name, nudge.TicketId,
                new SeriesWake(SeriesWakeKinds.Run, $"its run {attempt.RunId} ended {attempt.Status}" + (RunReportText.Headings(await ReportAsync(attempt.RunId, ct)) is { } headings ? $" — report: {headings}" : string.Empty))
                {
                    Once = $"run:{attempt.RunId}:{attempt.Status}", // 2026-11-25-96aec: an act's run end re-reads the same attempt
                    // 2026-11-25-96aea: a green run that left a pull request is no decision — the CI verdict that follows is
                    Quiet = attempt.Status == RunStatuses.Success && attempt.PullRequestUrls.Count > 0,
                }, ct);

    // The report only adds to the wake: an unreadable one costs its headings, never the wake.
    private async Task<string?> ReportAsync(string runId, CancellationToken ct)
    {
        try { return reports is null ? null : await reports.ReportAsync(runId, ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { return null; }
    }
}
