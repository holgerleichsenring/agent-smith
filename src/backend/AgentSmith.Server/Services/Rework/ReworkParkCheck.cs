using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9b: reads the ticket's CURRENT status from the tracker — never a webhook payload's,
/// which may be older than a move made since. A ticket is moved only when its status is not a trigger
/// status: Jira has no transition into the status a ticket is already in.
/// </summary>
public sealed class ReworkParkCheck(
    ITicketProviderFactory tickets, IUnmovedTicketStore unmovedTickets) : IReworkParkCheck
{
    public async Task<ReworkPark> CheckAsync(ResolvedProject project, string ticketId, CancellationToken cancellationToken)
    {
        var status = (await tickets.Create(project.Tracker).GetTicketAsync(new TicketId(ticketId), cancellationToken)).Status;
        var trigger = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type);
        var parked = trigger is not null && (Same(status, trigger.DoneStatus) || Same(status, trigger.FailedStatus))
            || await unmovedTickets.FindStandingAsync(project.Name, ticketId, project.Tracker.Name, cancellationToken) is not null;
        var statuses = trigger?.TriggerStatuses ?? [];
        var moveTo = statuses.Count > 0 && !statuses.Contains(status, StringComparer.OrdinalIgnoreCase)
            ? statuses[0] : null;
        return new ReworkPark(parked, moveTo);
    }

    public Task<bool> MoveAsync(ResolvedProject project, string ticketId, string status, CancellationToken cancellationToken) =>
        tickets.Create(project.Tracker).TransitionToAsync(new TicketId(ticketId), status, cancellationToken);

    private static bool Same(string? status, string? configured) =>
        !string.IsNullOrWhiteSpace(configured) && string.Equals(status, configured, StringComparison.OrdinalIgnoreCase);
}
