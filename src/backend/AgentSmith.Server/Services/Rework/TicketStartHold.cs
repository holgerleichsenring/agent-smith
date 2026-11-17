using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-16-f3fbc: what keeps agent-smith from starting a ticket by itself, lifted out of
/// <see cref="RequirementsMetStart"/> so a precondition release asks the same: a ticket a person marked done by
/// hand or closed (2026-10-15-114fa), and one the tracker counts closed. A tracker read failure holds it; the
/// next nudge reads again.
/// </summary>
public sealed class TicketStartHold(
    ITicketLandingStore landings,
    ITicketClosedMarkStore closed,
    ITicketProviderFactory trackers,
    ILogger<TicketStartHold> logger)
{
    /// <summary>True when a person marked the ticket done by hand or closed it here.</summary>
    public async Task<bool> MarkedAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var tracker = project.Tracker.Name;
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        if ((await landings.LandedAsync(tracker, [key], ct)).Count > 0)
        {
            logger.LogInformation("Ticket {Project}/{Ticket} was marked done by hand; not started", project.Name, ticketId);
            return true;
        }
        if ((await closed.OfAsync(tracker, [key], ct)).FirstOrDefault() is not { } mark) return false;
        logger.LogInformation("Ticket {Project}/{Ticket} was closed by {By}; not started", project.Name, ticketId, mark.By);
        return true;
    }

    /// <summary>True when the tracker counts the ticket open; false when it is closed or could not be read.</summary>
    public async Task<bool> OpenAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        try
        {
            if (!(await trackers.Create(project.Tracker).GetTicketAsync(new TicketId(ticketId), ct)).IsClosed) return true;
            logger.LogInformation("Ticket {Project}/{Ticket} is closed; not started", project.Name, ticketId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Project}/{Ticket} could not be read to start it", project.Name, ticketId);
        }
        return false;
    }
}
