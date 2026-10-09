using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0393a: the explicit operator Retry for a ticket parked on a NOT-IMPLEMENTABLE
/// verdict. A verdict does not auto-retry on a comment, so this is the only way
/// back: it clears the recorded hand-back state (otherwise the next attempt's
/// repeat guard would immediately read it as "handed back again with no progress")
/// and moves the ticket to a trigger status, where the existing poller claims it.
/// No second launch path — the one that already works is the one that runs.
/// <para>
/// 2026-09-21-1fa0: it MOVES before it CLEARS, and reports the tracker's own answer.
/// </para>
/// <para>
/// 2026-10-06-03c7f: it also clears the approval's SatisfiedAt, so a retried ticket is admitted
/// and discovered again.
/// </para>
/// </summary>
public sealed class NotImplementableRetryService(
    ITicketReopener reopener,
    ITicketProviderFactory ticketFactory,
    ILogger<NotImplementableRetryService> logger)
{
    public async Task<RetryOutcome> RetryAsync(
        ResolvedProject project, string ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var trigger = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type);
        var target = trigger?.TriggerStatuses.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(target))
        {
            logger.LogWarning(
                "Retry for {Project}/#{Ticket} has no trigger status to move the ticket to",
                project.Name, ticketId);
            return RetryOutcome.NoTriggerStatus;
        }
        // 2026-09-21-1fa0: THE MOVE COMES FIRST. The hold is the only thing left saying why a
        // ticket is not being claimed, so it is dropped ONLY once the ticket carries a status
        // the poller triggers on. Clearing it first meant a refused move — or a throwing one —
        // left the ticket in no trigger status with no hold: claimable by nobody and explained
        // by nothing. A throw now escapes HERE, before anything has been cleared.
        var moved = await ticketFactory.Create(project.Tracker)
            .TransitionToAsync(new TicketId(ticketId), target!, cancellationToken);
        if (!moved)
        {
            logger.LogWarning(
                "Retry for {Project}/#{Ticket}: the tracker offered no move to '{Status}' — "
                + "the ticket keeps its hold and was NOT retried", project.Name, ticketId, target);
            return RetryOutcome.TrackerRefusedTheMove;
        }
        // 2026-09-18-c1a7 / 2026-10-06-03c7f: the hand-back, the approval and the unmoved fact go
        // together — 2026-10-08-e8b9b: through the one reopener the rework entry uses too.
        await reopener.ClearAsync(project, ticketId, cancellationToken);
        logger.LogInformation(
            "Retry: {Project}/#{Ticket} moved back to '{Status}' and its hand-back state cleared",
            project.Name, ticketId, target);
        return RetryOutcome.Retried;
    }
}
