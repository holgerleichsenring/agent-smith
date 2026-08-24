using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503d: a role resolver built from one auth block, with the admin grant's environment
/// read STATED rather than set — the grant reaches the resolver through a captured
/// delegate, so a test says what is configured instead of mutating the process every
/// other test in the suite shares.
/// </summary>
internal static class ResolverUnderTest
{
    public static CallerIdentityResolver With(TokenAuthorityConfig auth, string? grant = null) =>
        new(auth, new CallerRoleReader(auth), new RoleCatalog(auth), Grant(grant));

    public static AdminGrant Grant(string? value, List<string>? asked = null) =>
        new(name =>
        {
            asked?.Add(name);
            return name == AdminGrant.EnvVar ? value : null;
        });

    /// <summary>
    /// An authenticated principal shaped the way the JwtBearer handler shapes one, with the
    /// configured name claim as its name type — which is what makes Identity.Name the
    /// claim the auth block chose.
    /// </summary>
    public static ClaimsPrincipal Caller(
        TokenAuthorityConfig auth, params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(
            [.. claims.Select(c => new Claim(c.Type, c.Value))],
            authenticationType: "Bearer",
            nameType: auth.NameClaim,
            roleType: ClaimTypes.Role));
}
