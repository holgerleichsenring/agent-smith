using System.Net;
using System.Security.Claims;
using AgentSmith.Server.Hubs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4dc: a caller who reads runs only through a project binding reaches the hub — the route
/// admits them because the hub's filter scopes every invocation — and meets the installation-wide system
/// feed as a refusal. Driven over long polling through SignalR's own dispatcher.
/// </summary>
[Trait("TestProcess", "env-2")]
[Collection(EnvVarCollection.Name)]
public sealed class ProjectHubScopeTests(ProjectScopeAuthorityFixture fixture)
    : IClassFixture<ProjectScopeAuthorityFixture>
{
    private const string HubPath = "/hub/jobs";

    [Fact]
    public async Task Enforce_SwitchOn_ProjectReader_NegotiatesTheHub()
    {
        using var request = fixture.As(AlphaTeam, HttpMethod.Post, $"{HubPath}/negotiate?negotiateVersion=1");

        var response = await fixture.Server.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "runs.read held in alpha alone admits the connection");
    }

    [Fact]
    public async Task Enforce_SwitchOn_ProjectReader_SubscribeOverview_Proceeds()
    {
        var completion = await Hub(AlphaTeam).InvokeAsync(nameof(JobsHub.SubscribeOverview));

        completion.Should().NotContain("\"error\"");
    }

    [Fact]
    public async Task Enforce_SwitchOn_ProjectReader_SubscribeSystem_IsRefused()
    {
        var completion = await Hub(AlphaTeam).InvokeAsync(nameof(JobsHub.SubscribeSystem));

        completion.Should().Contain("\"error\"").And.Contain("diagnostics.read");
    }

    private HubLongPoll Hub(string group) => new(
        fixture.Server.Client, HubPath,
        fixture.Issuer.Token(AuthorityFixture.Audience, extra: [new Claim("groups", group)], subject: $"{group}-person"));
}
