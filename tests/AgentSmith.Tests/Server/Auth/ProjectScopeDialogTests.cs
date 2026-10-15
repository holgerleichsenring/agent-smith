using System.Net;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4db: a dialog route's project is its conversation's. The ticket-bound widening stays, now
/// bounded by the project: a caller bound to alpha meets beta's conversations, uploads, mocks and images
/// as an empty 404, and a conversation grounded on no project yet carries nothing to refuse.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class ProjectScopeDialogTests(ProjectScopeAuthorityFixture fixture)
    : IClassFixture<ProjectScopeAuthorityFixture>
{
    private const string Mine = AlphaTeam + "-person";

    [Fact]
    public async Task ProjectScope_DialogOfAnotherProject_TicketBound_Is404()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-view-beta", "d-view-beta", "beta", ticketKey: "B-1"));
        await fixture.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-view-alpha", "d-view-alpha", "alpha", ticketKey: "A-1"));

        var beta = await SendAsync(HttpMethod.Get, "/api/spec-dialog/d-view-beta");
        var alpha = await SendAsync(HttpMethod.Get, "/api/spec-dialog/d-view-alpha");

        beta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await beta.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        alpha.StatusCode.Should().Be(HttpStatusCode.OK, "a ticket's conversation is anyone's who may reach the project");
    }

    [Fact]
    public async Task ProjectScope_DialogWithNoProjectYet_Passes()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-fresh", "d-fresh", "", owner: Mine));

        var response = await SendAsync(HttpMethod.Get, "/api/spec-dialog/d-fresh");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProjectScope_UploadNamingAnotherProjectInTheQuery_Is404()
    {
        var beta = await SendAsync(HttpMethod.Post, "/api/spec-dialog/images?dialogId=d-upload&project=beta");
        var alpha = await SendAsync(HttpMethod.Post, "/api/spec-dialog/images?dialogId=d-upload&project=alpha");

        beta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await beta.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        alpha.StatusCode.Should().NotBe(HttpStatusCode.NotFound, "the handler reads the empty upload itself");
    }

    [Fact]
    public async Task ProjectScope_MockOfASessionInAnotherProject_Is404()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-mock-beta", "d-mock-beta", "beta", owner: Mine));

        var response = await SendAsync(HttpMethod.Post, "/api/spec-dialog/conversations/s-mock-beta/mocks/spec-1/ratify");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ProjectScope_ImageOfASessionInAnotherProject_Is404()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-img-beta", "d-img-beta", "beta", owner: Mine));
        await fixture.SeedAsync(db => ProjectScopeRows.ImageAsync(db, 4401, "s-img-beta"));
        await fixture.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-img-alpha", "d-img-alpha", "alpha", owner: Mine));
        await fixture.SeedAsync(db => ProjectScopeRows.ImageAsync(db, 4402, "s-img-alpha"));

        var beta = await SendAsync(HttpMethod.Get, "/api/spec-dialog/images/4401");
        var alpha = await SendAsync(HttpMethod.Get, "/api/spec-dialog/images/4402");

        beta.StatusCode.Should().Be(HttpStatusCode.NotFound, "the caller owns it, and still holds nothing in beta");
        alpha.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path) =>
        fixture.Server.Client.SendAsync(fixture.As(AlphaTeam, method, path));
}
