using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503e — the control. An authority that answers leaves the caller's token as the only
/// thing that can be wrong, and the refusal has to keep saying so. Without this, "name the
/// server" could be implemented by never naming the token again.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ReachableAuthorityChallengeTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    [Fact]
    public async Task Challenge_AuthorityReachableAndTokenForged_StillNamesTheToken()
    {
        var forged = fixture.Issuer.Token(
            AuthorityFixture.Audience, issuer: "http://127.0.0.1:2");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/config/capabilities");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await fixture.Server.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle().Which
            .Parameter.Should().Contain("invalid_token",
                "the authority answered, so the token is the only thing left to blame");
    }
}
