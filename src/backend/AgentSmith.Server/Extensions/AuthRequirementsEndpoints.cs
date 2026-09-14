using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
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
        // 2026-08-25-1806: the refusal recorded for THIS request rides along — an enforcing
        // installation refuses /api/identity to the very caller whose token it rejected, so
        // this route is where "the server did not accept your token" can still be said.
        app.MapGet("/api/auth/requirements",
                (HttpContext ctx, [FromServices] TokenAuthorityConfig auth,
                    [FromServices] RefusedToken refused) =>
                {
                    Uncacheable(ctx.Response);
                    return Results.Ok(Answer(auth, refused, ctx));
                })
            .Anonymous("a caller with no token is the one who needs to read what a token must be");
        return app;
    }

    /// <summary>
    /// 2026-09-14-c72e: what this server expects, plus what THIS caller's refused token
    /// carried. The presented half is read only where a refusal was recorded, so an accepted
    /// token and an absent one are both answered with the installation's half alone.
    /// </summary>
    private static AuthRequirements Answer(
        TokenAuthorityConfig auth, RefusedToken refused, HttpContext ctx)
    {
        var reason = refused.Reason(ctx);
        var presented = reason is null ? null : refused.Presented(ctx);
        return AuthRequirements.From(auth) with
        {
            TokenRefusal = reason,
            PresentedAudience = presented?.Audience,
            PresentedIssuer = presented?.Issuer,
            PresentedTokenVersion = presented?.Version,
        };
    }

    /// <summary>
    /// 2026-09-14-c72e: the body was near-constant until it started carrying one caller's
    /// token fields. An anonymous route with no cache directive in front of an ingress, a CDN
    /// or a corporate proxy is how one caller's answer becomes the next caller's — so this one
    /// says both that it must not be stored and that it varies by the header it reads.
    /// </summary>
    private static void Uncacheable(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        response.Headers.Vary = "Authorization";
    }
}
