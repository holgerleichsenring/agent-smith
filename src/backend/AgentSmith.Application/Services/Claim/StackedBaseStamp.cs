using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-17-9a39b: a code claim, under the ticket's claim lock, asks what holds the ticket ONCE MORE and carries the
/// answer into the run: still held refuses with the gate's words, so a requirement that turned unmet between the gate
/// and the lock never cuts the dependent from the default branch; a stack tip is stamped as the run's base; none
/// removes a stamp a queued request carried from an earlier read.
/// </summary>
public static class StackedBaseStamp
{
    /// <summary>The request to claim with, or the refusal when the ticket is held now.</summary>
    public static async Task<(ClaimRequest Request, ClaimResult? Refusal)> ApplyAsync(
        ClaimRequest request, AgentSmithConfig config, UnmetTicketRequirements requirements, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(config);
        if (request.PipelineName != PipelinePresets.CodeName
            || !config.Projects.TryGetValue(request.ProjectName, out var project)) return (request, null);
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), request.TicketId.Value).Value;
        var hold = await requirements.ForStartAsync(project, key, ct);
        if (hold.Unmet.Count > 0)
            return (request, ClaimResult.Rejected(ClaimRejectionReason.TicketRequiresUnmet,
                RequiresUnmetGate.Sentence(hold.Unmet, hold.GreenLifts)));
        var context = new Dictionary<string, object>(request.InitialContext ?? []);
        var carried = context.Remove(ContextKeys.StackedOnTicket);
        if (hold.StackedOn is { } tip) context[ContextKeys.StackedOnTicket] = tip.RequiredTicketId;
        else if (!carried) return (request, null);
        return (request with { InitialContext = context.Count > 0 ? context : null }, null);
    }
}
