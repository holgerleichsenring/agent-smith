using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503e — who gets blamed when the authority was never there. The server booted against
/// a loopback port nothing listens on, with the enforce switch on: it has to answer at all,
/// and it has to say the fault is its own.
/// <para>
/// ONE booted server per test class, which is not a stylistic preference: xUnit initialises
/// a class's fixtures concurrently, and two boots racing over CONFIG_PATH give one of the
/// two servers the other's authority. The reachable half of this phase's assertions is a
/// separate class for that reason.
/// </para>
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class UnreachableAuthorityChallengeTests(UnreachableAuthorityFixture fixture)
    : IClassFixture<UnreachableAuthorityFixture>
{
    private const string PermissionedRoute = "/api/config/capabilities";

    [Fact]
    public async Task Startup_AuthorityUnreachable_HealthStillAnswers()
    {
        // The listener bound and answers with the authority dead from the first moment:
        // no route is short-circuited, and the boot never waited on the probe.
        var response = await fixture.Server.Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Challenge_AuthorityUnreachable_NamesTheServerNotTheToken()
    {
        await fixture.ProbeOnceAsync();

        var response = await Refused(fixture.Issuer.Token(AuthorityFixture.Audience, ["config.read"]));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var challenge = response.Headers.WwwAuthenticate.Should().ContainSingle().Subject;
        challenge.Scheme.Should().Be("Bearer", "a caller still has to recognise the scheme");
        challenge.Parameter.Should().NotContain("invalid_token",
            "the token was not rejected — the server never reached the authority to check it");
        challenge.Parameter.Should().Contain("temporarily_unavailable")
            .And.Contain("cannot reach its configured token authority");
    }

    private Task<HttpResponseMessage> Refused(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, PermissionedRoute);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return fixture.Server.Client.SendAsync(request);
    }
}
