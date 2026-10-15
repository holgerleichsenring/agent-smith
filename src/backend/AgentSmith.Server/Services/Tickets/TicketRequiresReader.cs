using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-14-dbcen: what one ticket waits for and what waits for it, each entry with the state a graph node
/// would show and its landing where one is stored. Requires entries carry no project, so each is read in the
/// page's project (contract decision 5: same project). By is shown as stored, with no lookup.
/// </summary>
public sealed class TicketRequiresReader(
    TicketListReader list, ITicketRequirementStore requirements, ITicketLandingStore landings, TicketProjects projects)
{
    /// <summary>Null only for a project the configuration does not name.</summary>
    public async Task<TicketRequiresView?> ReadAsync(string project, string ticketId, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return null;
        var tracker = resolved.Tracker.Name;
        var key = TicketKey.For(resolved.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var requires = (await requirements.RequiredOfAsync(tracker, key, ct))
            .Select(e => (Key: e.RequiredTicketKey, Id: e.RequiredTicketId)).ToList();
        var requiredBy = (await requirements.DependentsOfAsync(tracker, key, ct))
            .Select(e => (Key: e.TicketKey, Id: e.TicketId)).ToList();
        if (requires.Count == 0 && requiredBy.Count == 0) return new TicketRequiresView([], []);
        var summaries = TicketNodeStates.Summaries(await list.AllAsync(project, ct), p => projects.Of(p));
        var landed = await TicketNodeStates.LandedAsync(
            landings, requires.Concat(requiredBy).Select(e => (tracker, e.Key)), ct);
        TicketRequirementView Entry((string Key, string Id) e)
        {
            var summary = summaries.GetValueOrDefault((tracker, e.Key));
            var landing = landed.GetValueOrDefault((tracker, e.Key));
            var name = FiledTicketKey.Of(resolved.Tracker.Type, e.Id);
            return new TicketRequirementView(e.Id, name, summary?.Title ?? name, TicketNodeStates.Of(landing, summary),
                summary is not null, landing?.Kind, landing?.By, landing?.LandedAt, landing?.Note);
        }
        return new TicketRequiresView([.. requires.Select(Entry)], [.. requiredBy.Select(Entry)]);
    }
}
