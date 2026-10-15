using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4dd: the projects a list holds for this caller under one permission — every project when nothing
/// is enforced or the permission is held globally, else the configured projects a binding grants it in. A read
/// that already loaded the configuration hands its keys in, so the filter costs no second load.
/// </summary>
internal sealed class CallerProjectScope(ProjectAccess access)
{
    public ProjectScope For(ClaimsPrincipal user, string permission, IEnumerable<string>? configured = null)
    {
        if (!access.Enforcing) return ProjectScope.Everything;
        return access.ProjectsHolding(user, permission, configured) is { } projects
            ? ProjectScope.Of(projects)
            : ProjectScope.Everything;
    }
}
