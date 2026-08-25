using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b — the teeth. One booted server with one authority and the enforce switch on,
/// asked every way a caller can be wrong. The route under test is
/// <c>GET /api/config/capabilities</c>: it needs <c>config.read</c> and it answers without
/// a Redis, which the boot deliberately does not have.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class TokenAuthorityEnforcementTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    private const string PermissionedRoute = "/api/config/capabilities";
    private const string CrossingRoute = "/api/config/changes";

    [Fact]
    public async Task Endpoint_EnforceOnAndNoToken_Returns401() =>
        (await fixture.Server.Client.GetAsync(PermissionedRoute))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Endpoint_EnforceOnAndExpiredToken_Returns401() =>
        (await GetAsync(fixture.Issuer.ExpiredToken(AuthorityFixture.Audience)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Endpoint_EnforceOnAndWrongIssuer_Returns401() =>
        (await GetAsync(fixture.Issuer.Token(
            AuthorityFixture.Audience, issuer: "https://an-authority-nobody-configured")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Endpoint_EnforceOnAndWrongAudience_Returns401() =>
        (await GetAsync(fixture.Issuer.Token("some-other-service")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Endpoint_EnforceOnAndValidToken_ReachesTheHandler() =>
        (await GetAsync(TokenWith("config.read"))).StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task Policy_MissingOnePermission_Returns403NamingIt()
    {
        var response = await GetAsync(TokenWith("runs.read"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Missing(response)).Should().Equal("config.read");
    }

    [Fact]
    public async Task Policy_MissingOneOfTwoOnACrossingRoute_Returns403NamingOnlyTheMissingOne()
    {
        // The change feed states config.read AND secrets.read, because it returns the names
        // of changed secret entities. A holder of config.read alone is refused for exactly
        // one reason, and the body says which.
        var response = await GetAsync(TokenWith("config.read"), CrossingRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Missing(response)).Should().Equal("secrets.read");
    }

    [Fact]
    public async Task Enforce_SwitchOn_TheThirteenAnonymousRoutesStayReachable()
    {
        // "Reachable" is asserted as "not refused by the auth pipeline", not as a status
        // code: several of these answer other systems and refuse an empty body or an absent
        // signature themselves, which is the handler deciding — the point of the test.
        var refused = new List<string>();
        foreach (var (method, path) in AnonymousRoutes.FromGolden())
        {
            var response = await fixture.Server.Client.SendAsync(
                new HttpRequestMessage(new HttpMethod(method), path));
            if (WasRefusedByAuth(response)) refused.Add($"{method} {path} -> {(int)response.StatusCode}");
        }

        refused.Should().BeEmpty("p0503a declared these anonymous and the fallback policy "
            + "must exempt them without any route file being edited");
    }

    [Fact]
    public async Task Enforce_SwitchOn_APermissionedRouteIsStillRefused() =>
        WasRefusedByAuth(await fixture.Server.Client.GetAsync(PermissionedRoute))
            .Should().BeTrue("the control for the anonymous-route case: the same boot, "
                + "the same absence of a token, and a route that does state a permission");

    [Fact]
    public async Task Cors_PreflightToAPermissionedRoute_CarriesCorsHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, PermissionedRoute);
        request.Headers.Add("Origin", "http://127.0.0.1:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await fixture.Server.Client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "a browser that never gets CORS headers reports a network error, not a 401");
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }

    private static bool WasRefusedByAuth(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.Forbidden
        || (response.StatusCode == HttpStatusCode.Unauthorized
            && response.Headers.WwwAuthenticate.Any(h => h.Scheme == "Bearer"));

    private string TokenWith(params string[] permissions) =>
        fixture.Issuer.Token(AuthorityFixture.Audience, permissions);

    private Task<HttpResponseMessage> GetAsync(string token, string route = PermissionedRoute)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return fixture.Server.Client.SendAsync(request);
    }

    private static async Task<IReadOnlyList<string>> Missing(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ForbiddenPermissionResponse>())!.MissingPermissions;
}
