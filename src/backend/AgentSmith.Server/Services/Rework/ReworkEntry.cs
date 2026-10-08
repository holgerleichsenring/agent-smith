using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9b: makes a finished ticket claimable and starts one attempt. It does NOT carry the
/// act to the run: any claimer — this one, the poller, another replica's webhook — may win the lease,
/// so the run reads the act itself (ReworkActReader). Order: the live run, the newest attempt, the
/// act's time against it, the CURRENT status, the move, the clearing, the launch.
/// </summary>
public sealed class ReworkEntry(
    IActiveRunLease leases,
    IPreviousAttemptReader attempts,
    IReworkParkCheck parks,
    ITicketReopener reopener,
    IReworkLaunch launch,
    ILogger<ReworkEntry> logger) : IReworkEntry
{
    public async Task<ReworkOutcome> EnterAsync(
        ResolvedProject project, string ticketId, ReworkAct act, string pipeline, CancellationToken cancellationToken)
    {
        var attempt = await attempts.LatestAsync(project.Name, ticketId, excludingRunId: null, cancellationToken);
        var live = await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), cancellationToken);
        if (live is not null) return WhileLive(attempt, live.RunId, act);
        if (attempt is null) return ReworkOutcome.NotARework;
        if (attempt.Status == RunStatuses.WaitingForInput)
            return ReworkOutcome.Refused($"run {attempt.RunId} is waiting for an answer on this ticket", attempt.RunId);
        if (!attempt.Finished) return ReworkOutcome.NotARework;
        if (!attempt.Precedes(act.At)) return ReworkOutcome.AlreadyServed;
        return await ReopenAndLaunchAsync(project, ticketId, pipeline, cancellationToken);
    }

    // An act the live run already covers (no newer than its start) is served; a newer one is refused
    // with the run named, never queued behind it.
    private static ReworkOutcome WhileLive(PreviousAttempt? attempt, string? liveRunId, ReworkAct act) =>
        attempt is not null && attempt.RunId == liveRunId && !attempt.Precedes(act.At)
            ? ReworkOutcome.AlreadyServed
            : ReworkOutcome.Refused($"run {liveRunId ?? attempt?.RunId ?? "(starting)"} is working on this ticket", liveRunId);

    private async Task<ReworkOutcome> ReopenAndLaunchAsync(
        ResolvedProject project, string ticketId, string pipeline, CancellationToken cancellationToken)
    {
        var park = await parks.CheckAsync(project, ticketId, cancellationToken);
        if (!park.Parked) return ReworkOutcome.NotARework;
        if (park.MoveTo is { } target && !await parks.MoveAsync(project, ticketId, target, cancellationToken))
            return ReworkOutcome.Refused($"the tracker offered no move to '{target}'");
        await reopener.ClearAsync(project, ticketId, cancellationToken);
        var started = await launch.LaunchAsync(project.Name, ticketId, pipeline, cancellationToken);
        logger.LogInformation("Rework of {Project}/#{Ticket}: {Kind} {RunId}",
            project.Name, ticketId, started.Kind, started.RunId ?? started.Reason);
        return started;
    }
}
