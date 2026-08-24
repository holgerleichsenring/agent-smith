using System.Net;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0517: the one call p0503b's scope excluded and p0503c could not make. A browser cannot
/// set an Authorization header on a websocket handshake, so the token arrives in the query
/// string — and with nobody installing the reader, a hub connection could not authenticate
/// at all once the enforce switch went on.
/// <para>
/// Both cases run against the REAL registration on a booted server: the handler, its
/// events and its validation parameters are the ones the composition root configured, not
/// a rebuilt copy that could be configured differently.
/// </para>
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class HubHandshakeAuthenticationTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    private const string HubPath = "/hub/jobs";

    [Fact]
    public async Task Handshake_TokenOnTheHubPath_AuthenticatesTheConnection()
    {
        var token = fixture.Issuer.Token(AuthorityFixture.Audience, [Permissions.RunsRead]);

        var (onTheHub, _) = await AuthenticateAsync(HubPath, token);
        var (onARoute, _) = await AuthenticateAsync("/api/config/capabilities", token);
        var negotiate = await fixture.Server.Client.PostAsync(
            $"{HubPath}/negotiate?negotiateVersion=1&{HubHandshakeToken.ParameterName}={token}", null);
        var withoutToken = await fixture.Server.Client.PostAsync(
            $"{HubPath}/negotiate?negotiateVersion=1", null);

        onTheHub.Succeeded.Should().BeTrue("the hub's token arrives in the query string");
        onARoute.Succeeded.Should().BeFalse(
            "a query token is read on the hub path only; every other route uses the header");
        negotiate.StatusCode.Should().Be(HttpStatusCode.OK,
            "the handshake is what a connection is refused or admitted on");
        withoutToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the control: the same handshake with nothing to authenticate it");
    }

    [Fact]
    public async Task Handshake_AfterAuthentication_TheRequestNoLongerCarriesTheToken()
    {
        var token = fixture.Issuer.Token(AuthorityFixture.Audience, [Permissions.RunsRead]);

        var (result, request) = await AuthenticateAsync(
            HubPath, token, other: "negotiateVersion=1");

        result.Succeeded.Should().BeTrue();
        request.Should().NotContain(token, "nothing downstream of authentication needs it");
        request.Should().Be("?negotiateVersion=1", "and every other parameter survives");
    }

    /// <summary>
    /// Authenticates one request through the booted server's own JwtBearer handler and
    /// hands back the query string as it looks AFTERWARDS.
    /// </summary>
    private async Task<(AuthenticateResult Result, string Query)> AuthenticateAsync(
        string path, string token, string? other = null)
    {
        using var scope = fixture.Server.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(
            $"?{HubHandshakeToken.ParameterName}={token}" + (other is null ? "" : $"&{other}"));
        var result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        return (result, context.Request.QueryString.Value ?? string.Empty);
    }
}
