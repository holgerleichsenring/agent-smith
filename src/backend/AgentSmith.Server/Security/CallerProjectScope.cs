using AgentSmith.Contracts.Models.Configuration;
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
    /// <summary>2026-11-17-f3a8a: whether the installation's own conversation and runs exist for this caller.</summary>
    public bool SeesInstallation(ClaimsPrincipal user) => access.SeesInstallation(user);

    public ProjectScope For(ClaimsPrincipal user, string permission, IEnumerable<string>? configured = null)
    {
        if (!access.Enforcing) return ProjectScope.Everything;
        if (access.ProjectsHolding(user, permission, configured) is { } projects) return ProjectScope.Of(projects);
        // 2026-11-17-f3a8a: a global holder without the admin assistant's grant reads every project but the installation.
        return access.SeesInstallation(user) ? ProjectScope.Everything : ProjectScope.EverythingBut(InstallationScope.Name);
    }
}
