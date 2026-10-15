using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4db: the one place "may this caller do this, and where" is answered. Global
/// permissions hold everywhere; a project binding's hold only in its project, and only while the live
/// configuration still names that project — a project no longer configured is reachable through global
/// holds alone. Every enforcement site asks these members and never re-derives the union.
/// </summary>
internal sealed class ProjectAccess(
    TokenAuthorityConfig auth, CallerIdentityResolver identities, TicketProjects projects)
{
    /// <summary>Whether this installation refuses anything at all.</summary>
    public bool Enforcing => auth.Enforce;

    /// <summary>Every permission held globally, so in every project.</summary>
    public bool HoldsEverywhere(ClaimsPrincipal user, IReadOnlyList<string> permissions) =>
        AllIn(identities.Resolve(user).Permissions, permissions);

    /// <summary>The permission held globally or in at least one project.</summary>
    public bool HoldsAnywhere(ClaimsPrincipal user, string permission)
    {
        var identity = identities.Resolve(user);
        return identity.Permissions.Contains(permission, StringComparer.Ordinal)
               || identity.ProjectPermissions.Values.Any(held => held.Contains(permission, StringComparer.Ordinal));
    }

    /// <summary>Every permission held in this project: globally, or through a binding of a configured project.</summary>
    public bool HoldsIn(ClaimsPrincipal user, IReadOnlyList<string> permissions, string project)
    {
        var identity = identities.Resolve(user);
        return AllIn(identity.Permissions, permissions)
               || (IsConfigured(project) && AllIn(identity.PermissionsIn(project), permissions));
    }

    /// <summary>
    /// 2026-10-15-4a4dd: the configured projects this permission holds in, in configured spelling; null when it holds
    /// globally. The caller is resolved once and the configured keys read once (or taken as given), never per project.
    /// </summary>
    public IReadOnlyList<string>? ProjectsHolding(ClaimsPrincipal user, string permission, IEnumerable<string>? configured = null)
    {
        var identity = identities.Resolve(user);
        if (identity.Permissions.Contains(permission, StringComparer.Ordinal)) return null;
        return [.. (configured ?? projects.All().Keys)
            .Where(project => identity.PermissionsIn(project).Contains(permission, StringComparer.Ordinal))];
    }

    private bool IsConfigured(string project) => projects.All().Keys.Contains(project, ConfigNames.Comparer);

    private static bool AllIn(IReadOnlyList<string> held, IReadOnlyList<string> wanted) =>
        wanted.All(permission => held.Contains(permission, StringComparer.Ordinal));
}
