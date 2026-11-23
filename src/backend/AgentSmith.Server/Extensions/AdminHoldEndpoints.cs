using System.Security.Claims;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets.Holds;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-31dci: System → Needs attention — every admin hold across the projects the caller reads. config.write is
/// the admin's permission, as for Your move's admin holds (31dcg): it saves the setting an admin hold names. A caller
/// without it is refused by the permission layer, never by the route.
/// </summary>
internal static class AdminHoldEndpoints
{
    internal static WebApplication MapAdminHoldEndpoints(this WebApplication app)
    {
        // config.write is global-only, so the route declares no project; the rows are the caller's runs.read projects.
        app.MapGet("/api/holds/admin", ListAsync).Needs(Permissions.ConfigWrite);
        return app;
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> ListAsync(
        ClaimsPrincipal user, CallerProjectScope scopes, AdminHoldsReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ReadAsync(scopes.For(user, Permissions.RunsRead), cancellationToken));
}
