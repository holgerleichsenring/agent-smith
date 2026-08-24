using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: an authority configured, the enforce switch off. This is the state an operator
/// prepares an identity provider in, and it has to hold two things at once — nothing is
/// refused, and a token that IS presented is really validated. Either half alone would be
/// a lie: no validation is not "not enforcing", it is "not authenticating".
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class EnforceSwitchOffTests(PermissiveAuthorityFixture fixture)
    : IClassFixture<PermissiveAuthorityFixture>
{
    [Fact]
    public async Task Enforce_SwitchOffWithAnAuthorityConfigured_NoRouteIsRefused()
    {
        var refused = new List<string>();
        foreach (var path in PermissionedGetRoutes())
        {
            var response = await fixture.Server.Client.GetAsync(path);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                refused.Add($"GET {path} -> {(int)response.StatusCode}");
        }

        refused.Should().BeEmpty("the enforce switch is what refuses, and it is off");
    }

    [Fact]
    public async Task Enforce_SwitchOffWithAnAuthorityConfigured_AValidTokenIsStillValidated()
    {
        var valid = await AuthenticateAsync(fixture.Issuer.Token(AuthorityFixture.Audience));
        var wrongAudience = await AuthenticateAsync(fixture.Issuer.Token("some-other-service"));

        valid.Succeeded.Should().BeTrue("the scheme is registered and the authority answered");
        wrongAudience.Succeeded.Should().BeFalse(
            "a validator that accepts anything is not validating; the audience check proves it runs");
    }

    private async Task<AuthenticateResult> AuthenticateAsync(string token)
    {
        using var scope = fixture.Server.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token).ToString();
        return await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
    }

    // Every parameterless GET the golden marks as permissioned. Their handlers need a
    // database or a Redis this boot does not have, so the ANSWER is not the assertion —
    // the absence of a refusal before the handler is.
    private static IEnumerable<string> PermissionedGetRoutes() =>
        AnonymousRoutes.PermissionedParameterlessGets();
}
