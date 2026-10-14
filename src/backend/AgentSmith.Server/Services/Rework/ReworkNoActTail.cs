using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-8762v: the end of one nudge, lifted out of <see cref="ReworkNudgeHandler"/>. An unfinished attempt is
/// waited for; then the newest act is read. An act an operator's stop withholds is said once; a pending act is served
/// by the rework entry. With no act — none, or one already followed up — the ticket's series may go on by itself.
/// </summary>
public sealed class ReworkNoActTail(
    IReworkPendingActs pending, ClosedTicketActFollowUp followUps, ReworkWithheld withheld, ISeriesAutoStart series)
{
    public async Task<ReworkTail> ReadAsync(
        ResolvedProject project, ClaimedReworkNudge nudge, PreviousAttempt attempt, CancellationToken ct)
    {
        if (!attempt.Finished) return new(ReworkNudgeDisposition.Reschedule);
        var act = await pending.NewestAsync(project, nudge.TicketId, attempt, ct);
        if (await SpentAsync(project, nudge, act, ct)) return new(await series.ContinueAsync(project, nudge.TicketId, ct));
        if (await withheld.SaysAsync(project, nudge.TicketId, act!, attempt, ct)) return new(ReworkNudgeDisposition.Finish);
        return new(ReworkNudgeDisposition.Finish, act);
    }

    /// <summary>No act, or one already followed up on a new ticket.</summary>
    public async Task<bool> SpentAsync(ResolvedProject project, ClaimedReworkNudge nudge, PendingReworkAct? act, CancellationToken ct) =>
        act is null || await followUps.IsFollowedUpAsync(project, nudge.TicketId, act, ct);
}
