using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Preconditions;

/// <summary>
/// 2026-11-16-f3fbc: one pass over the tickets stuck on a precondition — a PreconditionsMet nudge for each whose
/// precondition now holds. The rework worker decides under the ticket's lock; the pass only nudges. A ticket whose
/// project is no longer configured is skipped, and one whose check throws is logged and asked again next pass.
/// </summary>
public sealed class PreconditionReleasePass(
    IStuckOnPreconditionReader stuck,
    PreconditionHoldCheck check,
    RepoReadiness readiness,
    IReworkNudges nudges,
    ILogger<PreconditionReleasePass> logger)
{
    /// <summary>Answers how many nudges the pass enqueued.</summary>
    public async Task<int> RunAsync(AgentSmithConfig config, bool epochStart, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var scope = new PreconditionPassScope(epochStart, new RepoReadinessPass(readiness));
        var nudged = 0;
        foreach (var ticket in await stuck.ListAsync(ct))
            if (config.Projects.TryGetValue(ticket.Project, out var project) && await HoldsAsync(ticket, project, scope, ct))
            {
                await nudges.EnqueueAsync(new ReworkNudgeRequest(ticket.Project, ticket.TicketId, ReworkNudgeOrigin.PreconditionsMet), ct);
                nudged++;
            }
        if (nudged > 0) logger.LogInformation("{Count} ticket(s) stuck on a precondition now hold it; nudged", nudged);
        return nudged;
    }

    private async Task<bool> HoldsAsync(StuckOnPrecondition ticket, ResolvedProject project, PreconditionPassScope scope, CancellationToken ct)
    {
        try
        {
            return await check.HoldsAsync(ticket, project, scope, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not check {Project}/{Ticket}'s {Kind} precondition; asked again next pass",
                ticket.Project, ticket.TicketId, ticket.EndKind);
            return false;
        }
    }
}
