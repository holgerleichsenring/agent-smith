using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-08-25-4530: the route on the installation it matters on — an authority configured
/// and the enforce switch on, which is the state where a dashboard configured with no
/// authority sees nothing but 401s. Booted rather than unit-tested, because the claim is
/// that a REAL enforcing server answers a caller who has nothing to present.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class AuthRequirementsRouteTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    private const string Route = "/api/auth/requirements";

    [Fact]
    public async Task Requirements_EnforcementOn_SaysSoAndNamesTheAuthority()
    {
        var requirements = await fixture.Server.Client.GetFromJsonAsync<AuthRequirements>(Route);

        requirements!.Enforced.Should().BeTrue();
        requirements.Authority.Should().Be(fixture.Issuer.Authority);
        requirements.Audience.Should().Be(AuthorityFixture.Audience);
    }

    [Fact]
    public async Task Requirements_Route_AnswersWithoutAToken()
    {
        var response = await fixture.Server.Client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "enforcement is on and no token was presented, which is exactly the caller "
            + "this route exists for — a route that refused them would answer the "
            + "question with the failure it is there to explain");
    }

    // The dashboard is the half that compares the two settings, so the names it reads by
    // are part of the contract and not a serializer default that may be reconfigured.
    [Fact]
    public async Task Requirements_TheAnswer_NamesItsFieldsAsTheDashboardReadsThem()
    {
        using var document = JsonDocument.Parse(
            await fixture.Server.Client.GetStringAsync(Route));

        document.RootElement.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo("enforced", "authority", "audience");
    }
}
