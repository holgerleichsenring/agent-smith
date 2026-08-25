using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-08-25-4530: the dashboard reads a refusal and an identity BY FIELD NAME off the
/// wire, so those names are part of the contract rather than a serializer default somebody
/// may reconfigure. The existing cases deserialize through the server's own record, which
/// stays green under any naming policy and would let a renamed field reach a browser that
/// then reports a refusal naming nothing.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class RefusalWireNamesTests(RoleMappingAuthorityFixture fixture)
    : IClassFixture<RoleMappingAuthorityFixture>
{
    [Fact]
    public async Task Forbidden_TheBody_NamesItsFieldsAsTheDashboardReadsThem()
    {
        var response = await GetAsync(
            Token(new Claim("roles", RoleMappingAuthorityFixture.CustomRole)),
            "/api/config/changes");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await FieldsOf(response)).Should().BeEquivalentTo("error", "missingPermissions");
    }

    [Fact]
    public async Task Identity_TheAnswer_NamesItsFieldsAsTheDashboardReadsThem()
    {
        var response = await GetAsync(Token(), "/api/identity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await FieldsOf(response)).Should().BeEquivalentTo(
            "authenticated", "subject", "issuer", "roleClaim", "groupClaim",
            "roleClaimValues", "groupClaimValues", "roles", "permissions", "findings");
    }

    private static async Task<IEnumerable<string>> FieldsOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
    }

    private string Token(params Claim[] claims) =>
        fixture.Issuer.Token(AuthorityFixture.Audience, extra: claims);

    private Task<HttpResponseMessage> GetAsync(string token, string route)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return fixture.Server.Client.SendAsync(request);
    }
}
