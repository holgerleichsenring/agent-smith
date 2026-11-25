using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-25-9992f: the requirement edges the series master writes between its tickets, keyed as filing keys
/// them. A new edge starts its dependent when met exactly as the dependent's existing edges do, and does when it has none.</summary>
public sealed class SeriesRequirementLinks(ITicketRequirementStore requirements, TimeProvider clock)
{
    /// <summary>The project's edges as they stand.</summary>
    public async Task<SeriesRequirementGraph> GraphAsync(ResolvedProject project, CancellationToken ct) =>
        new(await requirements.ByProjectAsync(project.Name, ct));

    /// <summary>Stores that <paramref name="dependentId"/> requires <paramref name="requiredIds"/>; answers how many were new.</summary>
    public async Task<int> AddAsync(
        ResolvedProject project, string conversation, string dependentId, IReadOnlyList<string> requiredIds, CancellationToken ct)
    {
        if (requiredIds.Count == 0) return 0;
        var key = Key(project, dependentId);
        var existing = await requirements.RequiredOfAsync(project.Tracker.Name, key, ct);
        var startsWhenMet = existing.Count == 0 || existing[0].StartsWhenMet;
        return await requirements.AddAsync([.. requiredIds.Distinct(StringComparer.Ordinal).Select(required => new TicketRequirementEdge(
            project.Tracker.Name, key, dependentId, project.Name, Key(project, required), required, startsWhenMet,
            clock.GetUtcNow(), conversation))], ct);
    }

    /// <summary>Removes that <paramref name="dependentId"/> requires <paramref name="requiredId"/>; answers whether it did.</summary>
    public Task<bool> RemoveAsync(ResolvedProject project, string dependentId, string requiredId, CancellationToken ct) =>
        requirements.RemoveAsync(project.Tracker.Name, Key(project, dependentId), Key(project, requiredId), ct);

    private static string Key(ResolvedProject project, string ticketId) => ApprovedSpecSetRecorder.KeyFor(project, ticketId).Value;
}
