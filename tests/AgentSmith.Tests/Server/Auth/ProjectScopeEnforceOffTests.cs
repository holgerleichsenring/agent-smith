using System.Net;
using System.Net.Http.Json;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4db: with the enforce switch off the project scope is not in the pipeline — a caller
/// bound to alpha only reads beta's run, the lists, and every handler's own answer, exactly as before.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class ProjectScopeEnforceOffTests(ProjectScopeEnforceOffFixture fixture)
    : IClassFixture<ProjectScopeEnforceOffFixture>
{
    [Fact]
    public async Task ProjectScope_EnforceOff_AnswersEveryRouteAsBeforeAndRegistersNoMiddleware()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-off-beta", "beta"));

        var run = await SendAsync(HttpMethod.Get, "/api/runs/r-off-beta");
        var list = await SendAsync(HttpMethod.Get, "/api/runs");
        var nowhere = await SendAsync(HttpMethod.Post, "/api/tickets/nowhere/T-1/landed-by-hand",
            JsonContent.Create(new { note = "" }));

        run.StatusCode.Should().Be(HttpStatusCode.OK);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        nowhere.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await nowhere.Content.ReadAsStringAsync()).Should().Be(
            "No project nowhere is configured.", "the handler answered, not the scope's empty 404");
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? body = null) =>
        fixture.Server.Client.SendAsync(fixture.As(AlphaTeam, method, path, body));
}
