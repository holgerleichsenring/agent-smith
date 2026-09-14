using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-09-14-c72e: a refusal named the CHECK that failed and never the value that failed
/// it, so an operator holding a token the server would not take had to decode it by hand to
/// find out how it differed. What the token carried now travels beside what the server
/// expected — read from the caller's own request, unverified, and only where a refusal was
/// recorded.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class PresentedTokenRouteTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    private const string Route = "/api/auth/requirements";

    /// <summary>The shape that motivated the phase: one directory, two access-token versions.</summary>
    private string V1ShapedToken() => fixture.Issuer.Token(
        $"api://{AuthorityFixture.Audience}", extra: [new Claim("ver", "1.0")]);

    [Fact]
    public async Task Requirements_TokenRefusedOnAudience_ReportsTheAudienceIssuerAndVersionItCarried()
    {
        var requirements = await RequirementsWith(V1ShapedToken());

        requirements.TokenRefusal.Should().Be(TokenRefusals.Audience);
        requirements.PresentedAudience.Should().Be($"api://{AuthorityFixture.Audience}");
        requirements.PresentedIssuer.Should().Be(fixture.Issuer.Authority,
            "the issuer half of the same mismatch is invisible in the refusal, which names "
            + "only the first check that failed");
        requirements.PresentedTokenVersion.Should().Be("1.0");
    }

    [Fact]
    public async Task Requirements_TokenRefused_EchoesNothingBeyondThoseThreeAndNeverTheToken()
    {
        var token = V1ShapedToken();

        var body = await (await Send(Route, token)).Content.ReadAsStringAsync();

        body.Should().NotContain(token, "the token is what the caller sent, not what we answer with");
        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Select(p => p.Name).Should().NotContain(
            ["sub", "permissions", "roles", "claims"],
            "only the fields the two sides are compared on are echoed");
    }

    [Fact]
    public async Task Requirements_TokenRefusedButUndecodable_ReportsTheRefusalWithoutPresentedValues()
    {
        var requirements = await RequirementsWith("this-is-not-a-token");

        requirements.TokenRefusal.Should().NotBeNull("something was presented and refused");
        requirements.PresentedAudience.Should().BeNull();
        requirements.PresentedIssuer.Should().BeNull();
        requirements.PresentedTokenVersion.Should().BeNull();
    }

    [Fact]
    public async Task Requirements_TokenAccepted_NothingAboutTheTokenIsEchoed()
    {
        var requirements = await RequirementsWith(fixture.Issuer.Token(AuthorityFixture.Audience));

        requirements.TokenRefusal.Should().BeNull();
        requirements.PresentedAudience.Should().BeNull(
            "an accepted token is not echoed back — the echo exists to explain a refusal");
        requirements.PresentedIssuer.Should().BeNull();
        requirements.PresentedTokenVersion.Should().BeNull();
    }

    [Fact]
    public async Task Requirements_NoTokenPresented_NothingAboutATokenIsEchoed()
    {
        var requirements = await fixture.Server.Client.GetFromJsonAsync<AuthRequirements>(Route);

        requirements!.PresentedAudience.Should().BeNull();
        requirements.PresentedIssuer.Should().BeNull();
        requirements.PresentedTokenVersion.Should().BeNull();
    }

    [Fact]
    public async Task Requirements_TwoCallers_OneRefused_TheOtherIsToldNothingAboutIt()
    {
        // The boundedness claim, as a mechanism rather than as an assertion about one
        // request: the refusal rides the HttpContext, so a second caller cannot be handed
        // the first one's values by a server that has just seen them.
        var refused = await RequirementsWith(V1ShapedToken());
        refused.PresentedAudience.Should().NotBeNull();

        var next = await fixture.Server.Client.GetFromJsonAsync<AuthRequirements>(Route);

        next!.PresentedAudience.Should().BeNull();
        next.PresentedIssuer.Should().BeNull();
        next.PresentedTokenVersion.Should().BeNull();
    }

    [Fact]
    public async Task Requirements_Response_IsNotCacheableAndVariesOnAuthorization()
    {
        var response = await Send(Route, V1ShapedToken());

        response.Headers.CacheControl!.NoStore.Should().BeTrue(
            "a body carrying one caller's token fields must not be held by any cache between here "
            + "and them");
        response.Headers.Vary.Should().Contain("Authorization");
    }

    private async Task<AuthRequirements> RequirementsWith(string token) =>
        (await (await Send(Route, token)).Content.ReadFromJsonAsync<AuthRequirements>())!;

    private Task<HttpResponseMessage> Send(string route, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return fixture.Server.Client.SendAsync(request);
    }
}

/// <summary>
/// 2026-09-14-c72e: the reader itself, without a server. It decodes a token it does NOT
/// validate, which is only defensible because the result is display for the person who sent
/// it — so what it must never do is throw, and what it must never be is a source of truth.
/// </summary>
public sealed class PresentedTokenTests
{
    [Fact]
    public void Read_NoAuthorizationHeader_IsNull() =>
        PresentedToken.Read(new DefaultHttpContext()).Should().BeNull();

    [Fact]
    public void Read_AHeaderThatIsNotABearer_IsNull() =>
        PresentedToken.Read(ContextWith("Basic dXNlcjpwYXNz")).Should().BeNull();

    [Fact]
    public void Read_ABearerThatIsNotAToken_IsNullRatherThanAThrow() =>
        PresentedToken.Read(ContextWith("Bearer not-a-token")).Should().BeNull();

    [Fact]
    public void Read_AnEmptyBearer_IsNull() =>
        PresentedToken.Read(ContextWith("Bearer ")).Should().BeNull();

    private static DefaultHttpContext ContextWith(string authorization)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = authorization;
        return context;
    }
}
