using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-11-23-933da: whether a caller may act on a PoC (933db onwards). Everyone when nothing is enforced; else
/// pocs.admin globally; else pocs.write held plus runs.read granted by a BINDING of the PoC's project — so a person
/// holding global reader and the poc role reads every PoC but acts only on their own.
/// </summary>
internal sealed class PocActAccess(ProjectAccess access)
{
    private static readonly string[] Admin = [Permissions.PocsAdmin];
    private static readonly string[] Write = [Permissions.PocsWrite];
    private static readonly string[] Read = [Permissions.RunsRead];

    public bool MayAct(ClaimsPrincipal user, Poc poc)
    {
        ArgumentNullException.ThrowIfNull(poc);
        if (!access.Enforcing || access.HoldsEverywhere(user, Admin)) return true;
        return access.HoldsEverywhere(user, Write) && access.HoldsByBindingIn(user, Read, poc.Project);
    }
}
