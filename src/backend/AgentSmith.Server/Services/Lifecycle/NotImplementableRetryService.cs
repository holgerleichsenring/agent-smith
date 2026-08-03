using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0393a: the explicit operator Retry for a ticket parked on a NOT-IMPLEMENTABLE
/// verdict. A verdict does not auto-retry on a comment, so this is the only way
/// back: it clears the recorded hand-back state (otherwise the next attempt's
/// repeat guard would immediately read it as "handed back again with no progress")
/// and moves the ticket to a trigger status, where the existing poller claims it.
/// No second launch path — the one that already works is the one that runs.
/// </summary>
public sealed class NotImplementableRetryService(
    ISpecSetPointerStore pointers,
    ITicketProviderFactory ticketFactory,
    ILogger<NotImplementableRetryService> logger)
{
    public async Task<bool> RetryAsync(
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
            return false;
        }
        await ClearHandbackAsync(project, ticketId, cancellationToken);
        await ticketFactory.Create(project.Tracker)
            .TransitionToAsync(new TicketId(ticketId), target!, cancellationToken);
        logger.LogInformation(
            "Retry: {Project}/#{Ticket} moved back to '{Status}' and its hand-back state cleared",
            project.Name, ticketId, target);
        return true;
    }

    private async Task ClearHandbackAsync(
        ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var platform = project.Tracker.Type.ToString().ToLowerInvariant();
        var key = SpecSetKey.For(platform, ticketId);
        var pointer = await pointers.GetAsync(project.Name, key.Value, ct);
        if (pointer is null) return;
        await pointers.SaveAsync(project.Name, pointer with
        {
            LastHandbackCase = SpecHandbackCase.None,
            RepeatedHandbackCount = 0,
            HandbackSourceSha = null,
        }, ct);
    }
}
