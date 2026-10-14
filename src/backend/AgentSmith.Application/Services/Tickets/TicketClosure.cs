using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59k: whether a PERSON closed a ticket for work. The tracker must count it closed, and
/// its status must not be one agent-smith sets (<see cref="TicketParkStatuses"/>). With no done_status
/// configured a successful run closes into the tracker's default close, which the status cannot tell
/// from a person's, so the history decides: the newest move into the current status by an actor that is
/// neither the token's identity nor an app. A close nobody can attribute reads not closed for work —
/// the ticket then reworks as before, where a wrong "closed" would file a follow-up nobody asked for.
/// </summary>
public sealed class TicketClosure(TrackerIdentity identity, ILogger<TicketClosure> logger)
{
    public async Task<bool> ClosedForWorkAsync(
        ResolvedProject project, ITicketProvider provider, Ticket ticket, CancellationToken cancellationToken)
    {
        if (!ticket.IsClosed) return false;
        var parks = TicketParkStatuses.Of(project);
        if (parks.Own(ticket.Status)) return false;
        return parks.Done is not null || await PersonClosedAsync(project, provider, ticket, cancellationToken);
    }

    private async Task<bool> PersonClosedAsync(
        ResolvedProject project, ITicketProvider provider, Ticket ticket, CancellationToken cancellationToken)
    {
        if (provider is not ITicketStatusHistory history) return false;
        try
        {
            if (await identity.SelfAsync(project.Tracker, cancellationToken) is not { } self) return false;
            var move = await history.NewestMoveIntoAsync(ticket.Id, ticket.Status, cancellationToken);
            return move is not null && !move.Actor.IsApp && !move.Actor.Is(self);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Who closed ticket {Ticket} of project {Project} could not be read; it reads not closed for work",
                ticket.Id.Value, project.Name);
            return false;
        }
    }
}
