using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503d: a role resolver built from one auth block, with the admin grant's environment
/// read STATED rather than set — the grant reaches the resolver through a captured
/// delegate, so a test says what is configured instead of mutating the process every
/// other test in the suite shares.
/// </summary>
internal static class ResolverUnderTest
{
    /// <summary>
    /// 2026-08-25-1806: a resolver whose mapping is still the bootstrap block's — the state
    /// an installation is in before its mapping has been migrated into the config store.
    /// </summary>
    public static CallerIdentityResolver With(TokenAuthorityConfig auth, string? grant = null) =>
        Resolver(new RoleMappingSource(new StoredMappingStub(null), auth), Grant(grant));

    /// <summary>
    /// A resolver whose mapping comes from the STORE, which is where it comes from once the
    /// migration has run. The stub is handed back so a test can change what is stored and
    /// assert the next resolve sees it.
    /// </summary>
    public static CallerIdentityResolver Over(
        TokenAuthorityConfig auth, StoredMappingStub stored, string? grant = null)
    {
        var source = new RoleMappingSource(stored, auth);
        source.AdoptStore();
        return Resolver(source, Grant(grant));
    }

    /// <summary>
    /// 2026-08-26-7a51: the resolver with its observation sink supplied. A test that cares
    /// what was noted passes its own; every other one gets a buffer nothing ever drains.
    /// </summary>
    public static CallerIdentityResolver Resolver(
        RoleMappingSource source, AdminGrant grant, ICallerObservations? observations = null) =>
        new(source, grant,
            observations ?? new CallerObservationBuffer(TimeProvider.System),
            TimeProvider.System, NullLogger<CallerIdentityResolver>.Instance);

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
