using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0503c: the handshake read is a pure function over HttpContext, which is what lets it
/// exist before the authentication pipeline that will call it — a bare
/// <see cref="DefaultHttpContext"/> is the whole rig.
/// </summary>
public sealed class HubHandshakeTokenTests
{
    private const string Token = "header.payload.signature";

    private static DefaultHttpContext Request(string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    [Fact]
    public void Handshake_TokenInTheQueryStringOnTheHubPath_IsRead()
    {
        var context = Request("/hub/jobs", $"?access_token={Token}");

        HubHandshakeToken.Read(context).Should().Be(Token);
    }

    [Fact]
    public void Handshake_TokenOnANonHubPath_IsNotRead()
    {
        var context = Request("/api/runs", $"?access_token={Token}");

        HubHandshakeToken.Read(context).Should().BeNull(
            "only the hub handshake has no way to send an Authorization header");
        context.Request.QueryString.Value.Should().Be($"?access_token={Token}",
            "a request this reader does not claim is left exactly as it arrived");
    }

    [Fact]
    public void Handshake_AfterTheRead_TheQueryStringNoLongerCarriesTheToken()
    {
        var context = Request("/hub/jobs/negotiate", $"?negotiateVersion=1&access_token={Token}");

        HubHandshakeToken.Read(context).Should().Be(Token);

        context.Request.QueryString.Value.Should().NotContain("access_token");
        context.Request.QueryString.Value.Should().NotContain(Token);
        context.Request.Query["access_token"].ToString().Should().BeEmpty();
    }

    [Fact]
    public void Handshake_OtherQueryParameters_SurviveTheRewrite()
    {
        var context = Request("/hub/jobs", $"?id=abc&access_token={Token}&negotiateVersion=1");

        HubHandshakeToken.Read(context);

        context.Request.Query["id"].ToString().Should().Be("abc");
        context.Request.Query["negotiateVersion"].ToString().Should().Be("1");
    }

    [Fact]
    public void Handshake_HubPathWithoutAToken_LeavesTheRequestAlone()
    {
        var context = Request("/hub/jobs", "?id=abc");

        HubHandshakeToken.Read(context).Should().BeNull();
        context.Request.QueryString.Value.Should().Be("?id=abc");
    }
}
