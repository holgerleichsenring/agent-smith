using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-16-f3fbc: a PreconditionsMet nudge, under the worker's lock on the ticket, after the handler's queued check.
/// A pending act is left to the rework entry as before. Otherwise the ticket's next attempt starts when the newest one
/// is still stuck on a precondition and nothing holds the ticket — a person marked it done by hand, closed it, or the
/// tracker counts it closed — through the funnel a series start takes, marked so its cause reads precondition. It
/// writes nothing to the tracker beyond what every claim writes; a refused launch is logged, and the next pass that
/// finds the precondition holding asks again.
/// </summary>
public sealed class PreconditionRelease(
    IPreviousAttemptReader attempts,
    IReworkPendingActs pending,
    ReworkNoActTail tail,
    TicketStartHold hold,
    IReworkLaunch launch,
    ILogger<PreconditionRelease> logger)
{
    /// <summary>The nudge's end, or null when the handler serves it as before: another origin, or an act waits.</summary>
    public async Task<ReworkNudgeDisposition?> ReleaseAsync(
        ResolvedProject project, ClaimedReworkNudge nudge, PreviousAttempt attempt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(nudge);
        ArgumentNullException.ThrowIfNull(attempt);
        if (nudge.Origin != ReworkNudgeOrigin.PreconditionsMet || !attempt.Finished) return null;
        var act = await pending.NewestAsync(project, nudge.TicketId, attempt, ct);
        return await tail.SpentAsync(project, nudge, act, ct) ? await StartAsync(project, nudge.TicketId, ct) : null;
    }

    private async Task<ReworkNudgeDisposition> StartAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var latest = await attempts.LatestAsync(project.Name, ticketId, null, ct);
        if (latest is not { StoppedOnPrecondition: true })
        {
            logger.LogInformation("Ticket {Project}/{Ticket} is no longer stuck on a precondition; not released", project.Name, ticketId);
            return ReworkNudgeDisposition.Finish;
        }
        if (await hold.MarkedAsync(project, ticketId, ct) || !await hold.OpenAsync(project, ticketId, ct))
            return ReworkNudgeDisposition.Finish;
        var outcome = await launch.StartPreconditionReleaseAsync(project.Name, ticketId, ct);
        if (outcome.Kind == ReworkOutcomeKind.Started)
            return outcome.RunId is null ? ReworkNudgeDisposition.Reschedule : ReworkNudgeDisposition.Finish;
        logger.LogWarning("Ticket {Project}/{Ticket}'s precondition ({Kind}) holds but its release was refused: {Reason}",
            project.Name, ticketId, latest.EndKind, outcome.Reason);
        return ReworkNudgeDisposition.Finish;
    }
}
