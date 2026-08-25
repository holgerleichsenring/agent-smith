using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Hosting;
using AgentSmith.Server.Services.Startup;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

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
            .AddJwtBearer(new JwtBearerSetup(auth).Apply);
        services.AddAuthorization(o =>
        {
            if (auth.Enforce) o.FallbackPolicy = EveryPermissionPolicy();
        });
        services.AddSingleton<IAuthorizationHandler, PermissionRequirementHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PermissionAuthorizationResultHandler>();
        // p0503e: handed THIS auth block rather than resolving one, because the registered
        // TokenAuthorityConfig is read lazily from the environment — a probe built later
        // can measure a different authority than the handler validates against, which is
        // the one thing its finding must never be wrong about.
        services.AddSingleton<IAuthorityReachability>(
            sp => ActivatorUtilities.CreateInstance<AuthorityReachabilityProbe>(sp, auth));
        services.AddSingleton<AuthorityAwareChallenge>();
        services.AddHostedService<AuthorityProbeHostedService>();
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
