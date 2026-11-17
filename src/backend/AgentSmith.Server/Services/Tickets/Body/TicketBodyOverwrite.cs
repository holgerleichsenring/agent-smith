using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Body;

/// <summary>
/// 2026-11-17-dd05b: a person asks for a ticket's body to say again what the specs on its branch say. The server
/// accepts and refuses; the writes are a 'ticket-body' run's step with a run id. A ticket a run holds is refused, as
/// an amendment is: the run reads the ticket and its fingerprint while it works.
/// </summary>
public sealed class TicketBodyOverwrite(
    TicketProjects projects, FiledWorkTrackerProjects trackerProjects, IServiceScopeFactory scopes,
    IActRun acts, ITicketActivityNudge activity)
{
    /// <summary>Null when the project is not configured; else how the overwrite ended.</summary>
    public async Task<TicketBodyOutcome?> OverwriteAsync(string project, string ticketId, string by, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return null;
        using (var scope = scopes.CreateScope())
        {
            if (await FiledWorkClaims.LeaseOnAsync(scope, trackerProjects.SharingTrackerWith(project), ticketId, ct) is { } claimed)
                return new(false, $"Ticket {ticketId} {claimed} Its body is not rewritten under a run that holds it — try again when no run holds it.");
        }
        var slot = new ActSlot<TicketBodyOutcome>();
        var result = await acts.RunAsync(project, PipelinePresets.TicketBodyName, new Dictionary<string, object>
        {
            [ContextKeys.TicketBodyRequest] = new TicketBodyRequest(ticketId, by),
            [ContextKeys.ActOutcome] = slot,
        });
        var outcome = slot.Value ?? new(false, $"The body of ticket {ticketId} was not rewritten: the ticket-body run ended first ({result.Message}).");
        if (outcome.Rewritten)
            await activity.ChangedAsync(project, TicketKey.For(resolved.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value, ct);
        return outcome;
    }
}
