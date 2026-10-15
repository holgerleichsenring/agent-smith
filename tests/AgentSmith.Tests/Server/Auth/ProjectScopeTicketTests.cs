using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4db: a ticket route names its project; a caller bound to alpha only meets a ticket of beta,
/// and of a project nobody configured, as the same empty 404 — and a global holder still reads every
/// handler's own answer.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ProjectScopeTicketTests(ProjectScopeAuthorityFixture fixture)
    : IClassFixture<ProjectScopeAuthorityFixture>
{
    [Fact]
    public async Task ProjectScope_TicketInAnotherProject_Is404WithAnEmptyBody()
    {
        var response = await SendAsync(AlphaTeam, HttpMethod.Get, "/api/tickets/beta/T-1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ProjectScope_TicketInAnotherProjectAndAnUnknownProject_AnswerTheSameBytes()
    {
        var beta = await LandAsync(AlphaTeam, "beta");
        var nowhere = await LandAsync(AlphaTeam, "nowhere");

        beta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        nowhere.StatusCode.Should().Be(beta.StatusCode);
        (await nowhere.Content.ReadAsByteArrayAsync()).Should().Equal(await beta.Content.ReadAsByteArrayAsync());
        nowhere.Content.Headers.ContentType.Should().Be(beta.Content.Headers.ContentType);
    }

    [Fact]
    public async Task ProjectScope_TicketOfTheBoundProject_ReachesItsHandler()
    {
        var response = await LandAsync(AlphaTeam, "alpha");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the handler refuses the missing note");
    }

    [Fact]
    public async Task ProjectScope_GlobalHolder_ReadsEveryProjectAndTheHandlersOwn404Text()
    {
        var beta = await LandAsync(Admins, "beta");
        var nowhere = await LandAsync(Admins, "nowhere");

        beta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        nowhere.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await nowhere.Content.ReadAsStringAsync()).Should().Be("No project nowhere is configured.");
    }

    [Fact]
    public async Task ProjectScope_TicketRouteWithAnEncodedSlashInTheProject_ChecksTheDecodedName()
    {
        var bound = await LandAsync(AlphaTeam, "team%2Falpha");
        var other = await LandAsync(AlphaTeam, "team%2Fbeta");

        bound.StatusCode.Should().Be(HttpStatusCode.BadRequest, "team/alpha is bound, read decoded as the handler reads it");
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/runs")]
    [InlineData("/api/tickets/graph")]
    public async Task ProjectScope_AcrossProjectsRoute_ProjectOnlyCaller_IsAdmittedAndFiltered(string path)
    {
        var response = await SendAsync(AlphaTeam, HttpMethod.Get, path);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "2026-10-15-4a4dd: every list filters by caller, so it opens");
    }

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/runs")]
    [InlineData("/api/projects")]
    public async Task ProjectScope_AcrossProjectsRoute_CallerHoldingThePermissionNowhere_Is403(string path)
    {
        var response = await SendAsync("nobody", HttpMethod.Get, path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private Task<HttpResponseMessage> LandAsync(string group, string project) =>
        fixture.Server.Client.SendAsync(fixture.As(group, HttpMethod.Post,
            $"/api/tickets/{project}/T-1/landed-by-hand", JsonContent.Create(new { note = "" })));

    private Task<HttpResponseMessage> SendAsync(string group, HttpMethod method, string path) =>
        fixture.Server.Client.SendAsync(fixture.As(group, method, path));
}
