using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-25-4530: what the server expects of a caller, over HTTP. Mapped beside the
/// findings route and unconditionally for the same reason — the channel that explains a
/// half-configured installation cannot be one of the things that installation configures.
/// <para>
/// ANONYMOUS is the point rather than a concession: a caller holding no token is precisely
/// the one who needs this answer, and a route that refused them would answer the question
/// with the failure it exists to explain.
/// </para>
/// <para>
/// The auth block comes from the container, which is the same bootstrap block
/// <see cref="Security.HubPermissionFilter"/> refuses off — so what this route reports and
/// what the server does are one value rather than two that can disagree.
/// </para>
/// </summary>
internal static class AuthRequirementsEndpoints
{
    internal static WebApplication MapAuthRequirementsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/auth/requirements",
                ([FromServices] TokenAuthorityConfig auth) => Results.Ok(AuthRequirements.From(auth)))
            .Anonymous("a caller with no token is the one who needs to read what a token must be");
        return app;
    }
}
