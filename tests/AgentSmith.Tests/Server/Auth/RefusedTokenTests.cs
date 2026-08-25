using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-08-25-1806: a token the server REFUSED used to be indistinguishable from one it
/// accepted that carried no role — both left an anonymous caller and an identity page of
/// empty lists. The two are fixed in different places, so the refusal is named, on the
/// anonymous route an enforcing installation still answers.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class RefusedTokenTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    private const string Route = "/api/auth/requirements";

    [Fact]
    public async Task Identity_TokenRefused_SaysSoRatherThanShowingAnEmptyMapping()
    {
        var requirements = await RequirementsWith(fixture.Issuer.Token("some-other-service"));

        requirements.TokenRefusal.Should().Be(TokenRefusals.Audience);
    }

    [Fact]
    public async Task Requirements_ExpiredToken_NamesTheExpiryRatherThanTheMapping()
    {
        var requirements = await RequirementsWith(
            fixture.Issuer.ExpiredToken(AuthorityFixture.Audience));

        requirements.TokenRefusal.Should().Be(TokenRefusals.Expired);
    }

    [Fact]
    public async Task Requirements_WrongIssuer_NamesTheIssuer()
    {
        var requirements = await RequirementsWith(fixture.Issuer.Token(
            AuthorityFixture.Audience, issuer: "https://an-authority-nobody-configured"));

        requirements.TokenRefusal.Should().Be(TokenRefusals.Issuer);
    }

    [Fact]
    public async Task Identity_TokenAcceptedWithNoRole_StillShowsWhatArrived()
    {
        // The control: a token this server ACCEPTED, carrying no role. Nothing was refused,
        // so the page has a mapping to write rather than an audience to fix.
        var accepted = fixture.Issuer.Token(AuthorityFixture.Audience, ["identity.read"]);
        var requirements = await RequirementsWith(accepted);
        requirements.TokenRefusal.Should().BeNull();

        var identity = await Get<CallerIdentity>("/api/identity", accepted);
        identity.Authenticated.Should().BeTrue();
        identity.Roles.Should().BeEmpty();
        identity.RoleClaim.Should().Be("roles", "the page names the claim it looked in");
    }

    [Fact]
    public async Task Requirements_NoTokenAtAll_RefusesNothing()
    {
        var requirements = await fixture.Server.Client.GetFromJsonAsync<AuthRequirements>(Route);

        requirements!.TokenRefusal.Should().BeNull(
            "a caller who presented nothing had nothing refused");
    }

    [Fact]
    public async Task Requirements_RefusedToken_StillAnswers()
    {
        var response = await Send(Route, fixture.Issuer.Token("some-other-service"));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "an enforcing installation answers /api/identity 401 to exactly this caller, "
            + "so this route is the only one that can carry the reason");
    }

    private async Task<AuthRequirements> RequirementsWith(string token) =>
        await Get<AuthRequirements>(Route, token);

    private async Task<T> Get<T>(string route, string token) =>
        (await (await Send(route, token)).Content.ReadFromJsonAsync<T>())!;

    private Task<HttpResponseMessage> Send(string route, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return fixture.Server.Client.SendAsync(request);
    }
}

/// <summary>
/// 2026-08-25-1806: the classification itself, without a server. What is handed to an
/// unauthenticated caller is which CHECK refused them, never the validation message — that
/// message names the values the check ran against and this route answers anybody.
/// </summary>
public sealed class RefusedTokenClassificationTests
{
    [Theory]
    [InlineData(typeof(SecurityTokenExpiredException), TokenRefusals.Expired)]
    [InlineData(typeof(SecurityTokenNotYetValidException), TokenRefusals.NotYetValid)]
    [InlineData(typeof(SecurityTokenInvalidAudienceException), TokenRefusals.Audience)]
    [InlineData(typeof(SecurityTokenInvalidIssuerException), TokenRefusals.Issuer)]
    [InlineData(typeof(SecurityTokenInvalidSignatureException), TokenRefusals.Signature)]
    [InlineData(typeof(SecurityTokenSignatureKeyNotFoundException), TokenRefusals.Signature)]
    [InlineData(typeof(SecurityTokenMalformedException), TokenRefusals.Malformed)]
    [InlineData(typeof(InvalidOperationException), TokenRefusals.Rejected)]
    public void Record_AFailure_ClassifiesItWithoutRepeatingTheMessage(Type failure, string expected)
    {
        var context = new DefaultHttpContext();
        var refused = new RefusedToken();

        refused.Record(context, (Exception)Activator.CreateInstance(failure, "IDX10214: the detail")!);

        refused.Reason(context).Should().Be(expected);
        refused.Reason(context).Should().NotContain("IDX10214");
    }

    [Fact]
    public void Record_SeveralChecksFailedAtOnce_NamesTheFirst()
    {
        var context = new DefaultHttpContext();
        var refused = new RefusedToken();

        refused.Record(context, new AggregateException(
            new SecurityTokenInvalidAudienceException(), new SecurityTokenExpiredException()));

        refused.Reason(context).Should().Be(TokenRefusals.Audience);
    }

    [Fact]
    public void Reason_NothingWasRefused_IsNull() =>
        new RefusedToken().Reason(new DefaultHttpContext()).Should().BeNull();
}
