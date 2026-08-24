using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0503b: the whole authentication pipeline, and the only place it is decided.
/// <para>
/// Registration hangs off the AUTHORITY: with none configured nothing here runs and the
/// server behaves exactly as it did before this phase. Refusal hangs off the ENFORCE
/// switch instead, through the fallback policy — with an authority configured and the
/// switch off, a presented token is still validated but no route is refused, which is
/// what keeps an installation reachable while its operator prepares an authority.
/// </para>
/// <para>
/// A fallback policy rather than a per-route convention, because there is nowhere to hang
/// a convention: this server maps its routes through fifteen independent extensions and
/// uses no route groups. The fallback reaches every one of them, and the twelve routes
/// p0503a declared anonymous are exempt because the authorization middleware skips any
/// endpoint carrying <c>IAllowAnonymous</c>.
/// </para>
/// </summary>
internal static class ServerAuthenticationExtensions
{
    internal static IServiceCollection AddServerAuthentication(
        this IServiceCollection services, TokenAuthorityConfig? auth)
    {
        if (auth is not { IsUsable: true }) return services;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o => Configure(o, auth));
        services.AddAuthorization(o =>
        {
            if (auth.Enforce) o.FallbackPolicy = EveryPermissionPolicy();
        });
        services.AddSingleton<IAuthorizationHandler, PermissionRequirementHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PermissionAuthorizationResultHandler>();
        return services;
    }

    /// <summary>
    /// Called explicitly and AFTER the map calls, because the automatic placement is wrong
    /// here: WebApplication inserts both straight after its own UseRouting, ahead of every
    /// user-registered middleware — and UseCors is registered from inside MapDashboardApi.
    /// A cross-origin preflight to a permissioned route would then be refused with 401 and
    /// no CORS headers, which is exactly how the dashboard reaches the server.
    /// </summary>
    internal static WebApplication UseServerAuthentication(
        this WebApplication app, TokenAuthorityConfig? auth)
    {
        if (auth is not { IsUsable: true }) return app;
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    private static void Configure(JwtBearerOptions options, TokenAuthorityConfig auth)
    {
        options.Authority = auth.Authority;
        // p0503d: OFF, or the role claim is invisible. The default inbound map rewrites
        // `roles` (and `role`) to the long WS-Federation role type, so a configured claim
        // name of `roles` finds ZERO claims — while `groups`, which is not in the map,
        // survives untouched. That asymmetry fails in production and passes in any unit
        // test that builds a ClaimsPrincipal directly, so it is pinned here.
        options.MapInboundClaims = false;
        // The discovery document of a loopback authority is served over plain HTTP; a
        // deployed one is not, and demanding metadata over HTTPS there is not optional.
        options.RequireHttpsMetadata = !IsLoopback(auth.Authority!);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // The issuer is not stated twice: the handler folds the authority's own
            // discovered issuer into these parameters, so the authority IS the issuer.
            ValidateIssuer = true,
            ValidateAudience = !string.IsNullOrWhiteSpace(auth.Audience),
            ValidAudience = auth.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            // p0503d: the claim a caller is NAMED by, so ClaimsPrincipal.Identity.Name is
            // the configured one — which is what the identity page shows and what a config
            // change is attributed to, through one accessor rather than two lookups.
            NameClaimType = auth.NameClaim,
        };
    }

    private static bool IsLoopback(string authority) =>
        Uri.TryCreate(authority, UriKind.Absolute, out var uri) && uri.IsLoopback;

    // One requirement per catalogued permission, so a refusal can name the ones that were
    // missing rather than the policy that failed.
    private static AuthorizationPolicy EveryPermissionPolicy() =>
        Permissions.All
            .Aggregate(
                new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser(),
                (builder, permission) => builder.AddRequirements(new PermissionRequirement(permission)))
            .Build();
}
