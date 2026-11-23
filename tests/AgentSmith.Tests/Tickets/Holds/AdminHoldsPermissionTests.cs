using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-31dci: System → Needs attention is the admin's — on a booted server with enforcement on, a caller
/// who reads runs but holds no config.write is refused by the permission layer, and one who holds it reaches the route.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class AdminHoldsPermissionTests(EnforcingAuthorityFixture fixture) : IClassFixture<EnforcingAuthorityFixture>
{
    private const string Route = "/api/holds/admin";

    [Fact]
    public async Task NeedsAttention_WithoutConfigWrite_IsForbidden_WithIt_IsNot()
    {
        var refused = await SendAsync(Permissions.RunsRead);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadFromJsonAsync<ForbiddenPermissionResponse>())!.MissingPermissions
            .Should().Equal(Permissions.ConfigWrite);

        var held = await SendAsync(Permissions.RunsRead, Permissions.ConfigWrite);

        held.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, "the refusal above is the permission layer's, not the route's");
    }

    private Task<HttpResponseMessage> SendAsync(params string[] permissions)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", fixture.Issuer.Token(AuthorityFixture.Audience, permissions));
        return fixture.Server.Client.SendAsync(request);
    }
}
