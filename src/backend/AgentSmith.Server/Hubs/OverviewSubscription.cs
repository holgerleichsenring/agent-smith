using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Tickets;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Hubs;

/// <summary>
/// 2026-10-15-4a4dc: what <c>SubscribeOverview</c> joins. With enforcement off, or for a caller who reads
/// runs globally, the one overview group and the installation's activity rollup, exactly as before; for a
/// caller who reads runs only through project bindings, the overview group of every configured project
/// they read in, and no rollup — it counts the whole installation's tracker traffic. Membership is decided
/// here, at subscribe time; a revoked binding takes effect on reconnect, the HTTP reads re-check each time.
/// </summary>
public sealed class OverviewSubscription
{
    private readonly TokenAuthorityConfig _auth;
    private readonly CallerIdentityResolver _identities;
    private readonly TicketProjects _projects;
    private readonly JobsBroadcaster _broadcaster;

    // Internal because the resolver is: the composition registers this through a factory.
    internal OverviewSubscription(
        TokenAuthorityConfig auth, CallerIdentityResolver identities, TicketProjects projects, JobsBroadcaster broadcaster)
    {
        _auth = auth;
        _identities = identities;
        _projects = projects;
        _broadcaster = broadcaster;
    }

    public async Task JoinAsync(HubCallerContext context, IGroupManager groups, ISingleClientProxy caller)
    {
        var identity = _auth.Enforce ? _identities.Resolve(context.User ?? new()) : null;
        if (identity is null || identity.Permissions.Contains(Permissions.RunsRead, StringComparer.Ordinal))
        {
            await groups.AddToGroupAsync(context.ConnectionId, HubGroups.Overview);
            await caller.SendAsync("SystemActivityUpdated", _broadcaster.GetSystemActivity());
            return;
        }
        foreach (var project in ReadableProjects(identity))
            await groups.AddToGroupAsync(context.ConnectionId, HubGroups.OverviewOf(project));
    }

    // Iterates the configuration, so a binding to a project no longer configured joins nothing.
    private IEnumerable<string> ReadableProjects(CallerIdentity identity) =>
        _projects.All().Keys.Where(project =>
            identity.ProjectPermissions.TryGetValue(project, out var held)
            && held.Contains(Permissions.RunsRead, StringComparer.Ordinal));
}
