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
/// act's time against it, the CURRENT status, the move, the clearing, the launch. A hand-back's waiting
/// attempt answered on the pull request: the status (the hand-back's accepted), the move, the row closed
/// as superseded, the clearing, the launch.
/// <para>2026-10-13-9a59t: a ticket a person closed is answered ClosedForWork before anything moves — a
/// waiting row it supersedes is closed first — and the handler files the follow-up.</para>
/// </summary>
public sealed class ReworkEntry(
    IActiveRunLease leases,
    IPreviousAttemptReader attempts,
    IReworkParkCheck parks,
    ITicketReopener reopener,
    IReworkLaunch launch,
    IWaitingRunCloser closer,
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
            : ReworkOutcome.WorkedOn(liveRunId ?? attempt?.RunId);

    public async Task<ReworkOutcome> AnswerHandbackAsync(
        ResolvedProject project, string ticketId, PreviousAttempt waiting, string pipeline, CancellationToken cancellationToken)
    {
        if (await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), cancellationToken) is { } live)
            return ReworkOutcome.WorkedOn(live.RunId);
        var park = await parks.CheckHandbackAsync(project, ticketId, cancellationToken);
        return await ReopenAndLaunchAsync(project, ticketId, pipeline, park, waiting.RunId, cancellationToken);
    }

    public async Task<ReworkOutcome?> SupersedeIfClosedAsync(
        ResolvedProject project, string ticketId, PreviousAttempt waiting, CancellationToken cancellationToken)
    {
        if (!(await parks.CheckAsync(project, ticketId, cancellationToken)).Closed) return null;
        await closer.CloseSupersededAsync(waiting.RunId, cancellationToken);
        return ReworkOutcome.ClosedForWork;
    }

    private async Task<ReworkOutcome> ReopenAndLaunchAsync(
        ResolvedProject project, string ticketId, string pipeline, CancellationToken cancellationToken) =>
        await ReopenAndLaunchAsync(project, ticketId, pipeline,
            await parks.CheckAsync(project, ticketId, cancellationToken), null, cancellationToken);

    private async Task<ReworkOutcome> ReopenAndLaunchAsync(ResolvedProject project, string ticketId, string pipeline,
        ReworkPark park, string? supersededRunId, CancellationToken cancellationToken)
    {
        if (park.Closed && supersededRunId is not null) await closer.CloseSupersededAsync(supersededRunId, cancellationToken);
        if (park.Closed) return ReworkOutcome.ClosedForWork;
        if (!park.Parked) return ReworkOutcome.NotARework;
        if (park.MoveTo is { } target && !await parks.MoveAsync(project, ticketId, target, cancellationToken))
            return ReworkOutcome.Refused($"the tracker offered no move to '{target}'");
        if (supersededRunId is not null) await closer.CloseSupersededAsync(supersededRunId, cancellationToken);
        await reopener.ClearAsync(project, ticketId, cancellationToken);
        var started = await launch.LaunchAsync(project.Name, ticketId, pipeline, cancellationToken);
        logger.LogInformation("Rework of {Project}/#{Ticket}: {Kind} {RunId}",
            project.Name, ticketId, started.Kind, started.RunId ?? started.Reason);
        return started;
    }
}
