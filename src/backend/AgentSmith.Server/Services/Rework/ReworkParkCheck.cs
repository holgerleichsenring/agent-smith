using AgentSmith.Application.Services.Tickets;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9b: reads the ticket's CURRENT status from the tracker — never a webhook payload's,
/// which may be older than a move made since. Reworkable: in done_status or failed_status, held by an
/// unmoved fact, or already in a trigger status (2026-10-08-e8b9c: a GitHub issue a successful run
/// labelled stays open); a verdict park is none of these — except for a pull-request act answering a
/// hand-back, whose clarification or not-implementable status is reworkable. A ticket is moved only out
/// of a non-trigger status: Jira has no transition into the status a ticket is already in.
/// </summary>
public sealed class ReworkParkCheck(
    ITicketProviderFactory tickets, IUnmovedTicketStore unmovedTickets) : IReworkParkCheck
{
    public Task<ReworkPark> CheckAsync(ResolvedProject project, string ticketId, CancellationToken cancellationToken) =>
        CheckAsync(project, ticketId, handback: false, cancellationToken);

    public Task<ReworkPark> CheckHandbackAsync(ResolvedProject project, string ticketId, CancellationToken cancellationToken) =>
        CheckAsync(project, ticketId, handback: true, cancellationToken);

    private async Task<ReworkPark> CheckAsync(ResolvedProject project, string ticketId, bool handback, CancellationToken cancellationToken)
    {
        var status = (await tickets.Create(project.Tracker).GetTicketAsync(new TicketId(ticketId), cancellationToken)).Status;
        var trigger = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type);
        var statuses = trigger?.TriggerStatuses ?? [];
        var parks = TicketParkStatuses.Of(project);
        var claimable = trigger is not null && (statuses.Count == 0 || statuses.Contains(status, StringComparer.OrdinalIgnoreCase));
        var parked = claimable
            || parks.IsRunEnd(status)
            || handback && parks.IsHandbackPark(status)
            || await unmovedTickets.FindStandingAsync(project.Name, ticketId, project.Tracker.Name, cancellationToken) is not null;
        return new ReworkPark(parked, parked && !claimable && statuses.Count > 0 ? statuses[0] : null);
    }

    public Task<bool> MoveAsync(ResolvedProject project, string ticketId, string status, CancellationToken cancellationToken) =>
        tickets.Create(project.Tracker).TransitionToAsync(new TicketId(ticketId), status, cancellationToken);
}
