using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-14-dbceh: a RequirementsMet nudge on a ticket no attempt worked, under the worker's lock on it. It
/// starts the ticket only when the gate would admit it — every requirement landed, the filing complete — and
/// its approver could start runs, and never one a person marked done by hand or closed: tagged or moved as
/// a filed ticket is, then claimed now through the poller's own route, since in webhook mode nothing else
/// delivers a ticket that already sits in a trigger status. A thrown exception reschedules the nudge.
/// </summary>
public sealed class RequirementsMetStart(
    ITicketRequirementStore requirements,
    ITicketLandingStore landings,
    UnmetTicketRequirements unmet,
    ITicketProviderFactory trackers,
    IServiceScopeFactory scopes,
    ReworkFallbackSpawn fallback,
    ILogger<RequirementsMetStart> logger)
{
    public async Task<ReworkNudgeDisposition> StartAsync(ResolvedProject project, ClaimedReworkNudge nudge, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(nudge);
        if (await HeldAsync(project, nudge.TicketId, ct) || !await OpenAsync(project, nudge.TicketId, ct))
            return ReworkNudgeDisposition.Finish;
        FiledWorkStart started;
        using (var scope = scopes.CreateScope())
            started = await scope.ServiceProvider.GetRequiredService<FiledWorkMove>()
                .StartExistingAsync(trackers.Create(project.Tracker), project, nudge.TicketId, mayStartRuns: true, ct);
        if (started.State == FiledStartState.Started) await fallback.SpawnAsync(project, nudge.TicketId, ct);
        else logger.LogWarning("Ticket {Project}/{Ticket}'s requirements landed but it was not started: {Reason}",
            project.Name, nudge.TicketId, started.Reason);
        return ReworkNudgeDisposition.Finish;
    }

    // Whether anything keeps the ticket from its start. An unmet requirement is silent: its landing wakes it again.
    private async Task<bool> HeldAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var tracker = project.Tracker.Name;
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var edges = await requirements.RequiredOfAsync(tracker, key, ct);
        if (edges.Count == 0) return true;
        if ((await landings.LandedAsync(tracker, [key], ct)).Count > 0)
        {
            logger.LogInformation("Ticket {Project}/{Ticket} was marked done by hand; not started", project.Name, ticketId);
            return true;
        }
        if ((await unmet.OfAsync(tracker, key, ct)).Unmet.Count > 0) return true;
        if (!await unmet.FiledCompletelyAsync(tracker, key, ct))
        {
            logger.LogWarning("Ticket {Project}/{Ticket}'s filing is incomplete; not started", project.Name, ticketId);
            return true;
        }
        if (edges.All(e => e.StartsWhenMet)) return false;
        logger.LogInformation("Ticket {Project}/{Ticket}'s requirements landed; it waits for a start by name", project.Name, ticketId);
        return true;
    }

    // A ticket the tracker counts closed was closed by a person; a read failure is retried by the re-read.
    private async Task<bool> OpenAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
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
