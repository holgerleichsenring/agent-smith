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
    TicketStartHold hold, // 2026-11-16-f3fbc: the landing, closed-mark and tracker-closed checks, shared with the release
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
        if (await HeldAsync(project, nudge.TicketId, ct) || !await hold.OpenAsync(project, nudge.TicketId, ct))
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
        if (await hold.MarkedAsync(project, ticketId, ct)) return true;
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
}
