using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503d, end to end: one booted server with an authority, the enforce switch on, and an
/// auth block that maps claims onto roles. These are the assertions a unit test over a
/// hand-built ClaimsPrincipal cannot make — the claims go through the real JwtBearer
/// handler, which is where the inbound claim map would have eaten the role claim.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class IdentityEndpointTests(RoleMappingAuthorityFixture fixture)
    : IClassFixture<RoleMappingAuthorityFixture>
{
    private const string IdentityRoute = "/api/identity";
    private const string CrossingRoute = "/api/config/changes";

    [Fact]
    public async Task Roles_RoleClaimNamedRoles_IsFoundWithInboundMappingOff()
    {
        var identity = await Identity(Token(new Claim("roles", BuiltInRoles.Reader)));

        identity.RoleClaimValues.Should().Equal(BuiltInRoles.Reader);
        identity.Roles.Should().ContainSingle(
            "MapInboundClaims rewrites 'roles' to the WS-Federation role type by default, "
            + "which leaves a configured claim name of 'roles' finding nothing")
            .Which.Should().Be(BuiltInRoles.Reader);
    }

    [Fact]
    public async Task Identity_CallerWithZeroRoles_IsNotRefusedByThePermissionPolicy()
    {
        var response = await GetAsync(Token(), IdentityRoute);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the page exists for the caller who holds nothing yet");
    }

    [Fact]
    public async Task Identity_CallerWithZeroRoles_SeesItsRawClaimValues()
    {
        var identity = await Identity(Token(new Claim("groups", "a-group-nobody-mapped")));

        identity.Roles.Should().BeEmpty();
        identity.GroupClaimValues.Should().Equal("a-group-nobody-mapped");
        identity.GroupClaim.Should().Be("groups", "the claim that was looked in is named too");
        identity.Subject.Should().NotBeNullOrEmpty();
        identity.Issuer.Should().Be(fixture.Issuer.Authority);
        identity.Permissions.Should().Equal(Permissions.IdentityRead);
    }

    [Fact]
    public async Task Identity_CallerWithRoles_SeesResolvedRolesAndEffectivePermissions()
    {
        var identity = await Identity(Token(
            new Claim("groups", $"/{RoleMappingAuthorityFixture.MappedGroup}")));

        identity.Roles.Should().Equal(BuiltInRoles.Operator);
        identity.Permissions.Should().Contain([Permissions.RunsControl, Permissions.ProjectsInit]);
        identity.Permissions.Should().NotContain(Permissions.ConfigWrite);
    }

    // A custom role is a subset of the catalog, and the config/secrets split holds against
    // it exactly as it holds against a built-in one.
    [Fact]
    public async Task CustomRole_HoldingConfigReadOnly_OnTheChangeFeed_Returns403NamingSecretsReadOnly()
    {
        var response = await GetAsync(
            Token(new Claim("roles", RoleMappingAuthorityFixture.CustomRole)), CrossingRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<ForbiddenPermissionResponse>();
        body!.MissingPermissions.Should().Equal(Permissions.SecretsRead);
    }

    private string Token(params Claim[] claims) =>
        fixture.Issuer.Token(AuthorityFixture.Audience, extra: claims);

    private async Task<CallerIdentity> Identity(string token)
    {
        var response = await GetAsync(token, IdentityRoute);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CallerIdentity>())!;
    }

    private Task<HttpResponseMessage> GetAsync(string token, string route)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return fixture.Server.Client.SendAsync(request);
    }
}
