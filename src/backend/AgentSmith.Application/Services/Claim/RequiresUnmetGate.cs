using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-10-14-dbceh: refuses a code claim of a ticket a ticket it requires has not landed for, or whose
/// filing did not finish. Strict: no start by name and no series auto-start passes it — marking the
/// required ticket done by hand lifts it everywhere at once. A ticket without requirement rows costs one read.
/// </summary>
public sealed class RequiresUnmetGate(UnmetTicketRequirements requirements)
{
    public const string IncompleteFiling = "This ticket's filing did not finish; close it and approve the list again.";

    /// <summary>The refusal, or null when nothing the ticket requires holds it.</summary>
    public async Task<ClaimResult?> RefusalAsync(ClaimRequest request, AgentSmithConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(config);
        if (request.PipelineName != PipelinePresets.CodeName
            || !config.Projects.TryGetValue(request.ProjectName, out var project)) return null;
        var tracker = project.Tracker;
        var key = TicketKey.For(tracker.Type.ToString().ToLowerInvariant(), request.TicketId.Value).Value;
        var (hasRequirements, unmet) = await requirements.OfAsync(tracker.Name, key, cancellationToken);
        if (!hasRequirements) return null;
        if (!await requirements.FiledCompletelyAsync(tracker.Name, key, cancellationToken))
            return ClaimResult.Rejected(ClaimRejectionReason.TicketRequiresUnmet, IncompleteFiling);
        return unmet.Count == 0 ? null : ClaimResult.Rejected(ClaimRejectionReason.TicketRequiresUnmet, Sentence(unmet));
    }

    /// <summary>What the ticket waits for, each required ticket named "#n" when its id is numeric and as written otherwise.</summary>
    public static string Sentence(IReadOnlyList<TicketRequirementEdge> unmet)
    {
        ArgumentNullException.ThrowIfNull(unmet);
        var named = string.Join(", ", unmet.Select(e => Named(e.RequiredTicketId)));
        return $"This ticket waits for {named} to be merged first; merge them, or mark them done by hand.";
    }

    private static string Named(string id) => id.Length > 0 && id.All(char.IsAsciiDigit) ? $"#{id}" : id;
}
