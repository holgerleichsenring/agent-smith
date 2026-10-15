using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-10-15-4a4dc: the groups an overview nudge goes to — the overview group, where global readers sit,
/// plus the group of the project it is about, where that project's readers sit. A run belongs to one
/// project and no connection sits in both kinds, so nobody hears a nudge twice. With enforcement off every
/// answer is the overview group alone and nothing is read: an installation that never enforced sends
/// exactly what it sent before.
/// </summary>
public sealed class OverviewAudience(TokenAuthorityConfig auth, RunProjectIndex runs, TicketProjects projects)
{
    private static readonly IReadOnlyList<string> OverviewAlone = [HubGroups.Overview];

    public IReadOnlyList<string> OfProject(string? project) =>
        !auth.Enforce || string.IsNullOrEmpty(project) ? OverviewAlone : [HubGroups.Overview, HubGroups.OverviewOf(project)];

    /// <summary>A run's audience, reading its project through and remembering nothing.</summary>
    public async Task<IReadOnlyList<string>> OfRunAsync(string runId, string? known, CancellationToken ct) =>
        auth.Enforce ? OfProject(await runs.OfAsync(runId, known, ct)) : OverviewAlone;

    /// <summary>A live, unfinished run's audience — the fan-out's alone, whose project the index remembers.</summary>
    public async Task<IReadOnlyList<string>> OfLiveRunAsync(string runId, string? known, CancellationToken ct) =>
        auth.Enforce ? OfProject(await runs.LiveAsync(runId, known, ct)) : OverviewAlone;

    /// <summary>Every reader of every configured project — for a nudge that names no run.</summary>
    public IReadOnlyList<string> Everyone() =>
        auth.Enforce ? [HubGroups.Overview, .. projects.All().Keys.Select(HubGroups.OverviewOf)] : OverviewAlone;
}
