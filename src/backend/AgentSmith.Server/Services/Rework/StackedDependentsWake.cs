using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-17-9a39a: a prerequisite that pushed a green branch wakes the tickets that wait for it, under
/// <see cref="RequiresStart.OnGreenPush"/> — one RequirementsMet nudge per dependent that is ready to start, served by
/// <see cref="RequirementsMetStart"/> as a landing's wake is. Asked on every nudge of a ticket whose newest attempt is
/// finished, so the prerequisite's own RunEnd nudge carries it.
/// <para>A failed read or enqueue THROWS: the durable nudge it rides is rescheduled, which is its recovery — there is
/// no landing row a periodic re-read could re-derive a green wake from.</para>
/// </summary>
public sealed class StackedDependentsWake(
    ITicketRequirementStore requirements,
    ITicketClosedMarkStore closed,
    UnmetTicketRequirements unmet,
    IPreviousAttemptReader attempts,
    IActiveRunLease leases,
    IReworkNudges nudges,
    ILogger<StackedDependentsWake> logger)
{
    /// <summary>Nudges every ready dependent of a ticket whose attempt is green; answers how many.</summary>
    public async Task<int> WakeAsync(ResolvedProject project, string ticketId, PreviousAttempt attempt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.RequiresStart != RequiresStart.OnGreenPush || !GreenPrerequisites.IsGreen(attempt)) return 0;
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var woken = 0;
        foreach (var edge in (await requirements.DependentsOfAsync(project.Tracker.Name, key, ct))
                     .DistinctBy(e => e.TicketKey, StringComparer.Ordinal))
        {
            if (!await ReadyAsync(project, edge, ct)) continue;
            await nudges.EnqueueAsync(new ReworkNudgeRequest(edge.Project, edge.TicketId, ReworkNudgeOrigin.RequirementsMet), ct);
            woken++;
        }
        if (woken > 0)
            logger.LogInformation("The green branch of {Project}/{Ticket} woke {Count} waiting ticket(s)", project.Name, ticketId, woken);
        return woken;
    }

    // DependentsWake's readiness, asked as a start asks it: the project's mode lifts green requirements.
    private async Task<bool> ReadyAsync(ResolvedProject project, TicketRequirementEdge edge, CancellationToken ct)
    {
        if (edge.Project != project.Name) return false;
        if (!(await requirements.RequiredOfAsync(edge.Tracker, edge.TicketKey, ct)).All(e => e.StartsWhenMet)) return false;
        if ((await closed.OfAsync(edge.Tracker, [edge.TicketKey], ct)).Count > 0) return false;
        if ((await unmet.ForStartAsync(project, edge.TicketKey, ct)).Unmet.Count > 0) return false;
        return await unmet.FiledCompletelyAsync(edge.Tracker, edge.TicketKey, ct)
            && await attempts.LatestAsync(edge.Project, edge.TicketId, null, ct) is null
            && !await attempts.HasQueuedAsync(edge.Project, edge.TicketId, ct)
            && await leases.GetByTicketAsync(edge.Project, new TicketId(edge.TicketId), ct) is null;
    }
}
