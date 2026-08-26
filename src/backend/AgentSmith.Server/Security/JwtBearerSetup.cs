using AgentSmith.Contracts.Models.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503b: how ONE configured authority is turned into JwtBearer options. Split out of
/// ServerAuthenticationExtensions (2026-08-25-1806), which decides WHETHER the pipeline is
/// registered at all — a separate decision from what the handler then validates.
/// <para>
/// Built at the composition root with the auth block in hand rather than resolved: the
/// options delegate runs before the container is usable, and the block this validates
/// against has to be the same instance the registration was given.
/// </para>
/// </summary>
internal sealed class JwtBearerSetup(TokenAuthorityConfig auth)
{
    public void Apply(JwtBearerOptions options)
    {
        options.Authority = auth.Authority;
        // p0517: a browser cannot set an Authorization header on a websocket handshake, so
        // the hub's token arrives in the query string and this is where it is picked up.
        options.Events ??= new JwtBearerEvents();
        options.Events.OnMessageReceived = HubHandshakeAuthentication.Receive;
        // p0503d: OFF, or the role claim is invisible. The default inbound map rewrites
        // `roles` (and `role`) to the long WS-Federation role type, so a configured claim
        // name of `roles` finds ZERO claims — while `groups`, which is not in the map,
        // survives untouched. That asymmetry fails in production and passes in any unit
        // test that builds a ClaimsPrincipal directly, so it is pinned here.
        options.MapInboundClaims = false;
        // The discovery document of a loopback authority is served over plain HTTP; a
        // deployed one is not, and demanding metadata over HTTPS there is not optional.
        options.RequireHttpsMetadata = !IsLoopback(auth.Authority!);
        options.TokenValidationParameters = Validation();
        // p0503e: the challenge, and nothing else on this event set — a sibling adding
        // another handler assigns its own property rather than replacing this one.
        options.Events.OnChallenge = context => context.HttpContext
            .RequestServices.GetRequiredService<AuthorityAwareChallenge>().WriteAsync(context);
        // 2026-08-25-1806: a refused token leaves an anonymous principal and nothing else,
        // so the one moment the reason exists is here. It is recorded on the request; the
        // anonymous requirements route is where a caller reads it back.
        options.Events.OnAuthenticationFailed = Record;
    }

    private TokenValidationParameters Validation() => new()
    {
        // The issuer is not stated twice: the handler folds the authority's own discovered
        // issuer into these parameters, so the authority IS the issuer.
        ValidateIssuer = true,
        ValidateAudience = !string.IsNullOrWhiteSpace(auth.Audience),
        ValidAudience = auth.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        // p0503d: the claim a caller is NAMED by, so ClaimsPrincipal.Identity.Name is the
        // configured one — which is what the identity page shows and what a config change
        // is attributed to, through one accessor rather than two lookups.
        NameClaimType = auth.NameClaim,
    };

    private static Task Record(AuthenticationFailedContext context)
    {
        context.HttpContext.RequestServices.GetRequiredService<RefusedToken>()
            .Record(context.HttpContext, context.Exception);
        return Task.CompletedTask;
    }

    private static bool IsLoopback(string authority) =>
        Uri.TryCreate(authority, UriKind.Absolute, out var uri) && uri.IsLoopback;
}
