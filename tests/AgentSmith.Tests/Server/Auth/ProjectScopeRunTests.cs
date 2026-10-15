using System.Net;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4db: a run route's project is the run's stored one. A caller bound to alpha reads and
/// cancels alpha's runs; beta's run, and a run that does not exist, are the same empty 404.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class ProjectScopeRunTests(ProjectScopeAuthorityFixture fixture)
    : IClassFixture<ProjectScopeAuthorityFixture>
{
    [Fact]
    public async Task ProjectScope_RunOfAnotherProject_IsNotCancelledAndIs404()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-beta-1", "beta"));

        var cancel = await SendAsync(AlphaTeam, HttpMethod.Post, "/api/runs/r-beta-1/cancel");
        var read = await SendAsync(AlphaTeam, HttpMethod.Get, "/api/runs/r-beta-1");

        cancel.StatusCode.Should().Be(HttpStatusCode.NotFound);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await read.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        (await CancelRequestedAsync("r-beta-1")).Should().BeFalse("the handler never ran");
    }

    [Fact]
    public async Task ProjectScope_RunOfTheBoundProject_IsReadAndCancelled()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-alpha-1", "alpha"));

        var read = await SendAsync(AlphaTeam, HttpMethod.Get, "/api/runs/r-alpha-1");
        var cancel = await SendAsync(AlphaTeam, HttpMethod.Post, "/api/runs/r-alpha-1/cancel");

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        cancel.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await CancelRequestedAsync("r-alpha-1")).Should().BeTrue();
    }

    [Fact]
    public async Task ProjectScope_UnknownRunId_ForAProjectOnlyCaller_IsTheSame404()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-beta-2", "beta"));

        var unknown = await SendAsync(AlphaTeam, HttpMethod.Get, "/api/runs/r-none/trail");
        var foreign = await SendAsync(AlphaTeam, HttpMethod.Get, "/api/runs/r-beta-2/trail");

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknown.Content.ReadAsByteArrayAsync()).Should().Equal(await foreign.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ProjectScope_GlobalHolder_ReadsARunOfEveryProject()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-beta-3", "beta"));

        var read = await SendAsync(Admins, HttpMethod.Get, "/api/runs/r-beta-3");

        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> SendAsync(string group, HttpMethod method, string path) =>
        fixture.Server.Client.SendAsync(fixture.As(group, method, path));

    private async Task<bool> CancelRequestedAsync(string runId)
    {
        var requested = false;
        await fixture.SeedAsync(async db => requested =
            await db.Set<Run>().AsNoTracking().Where(r => r.Id == runId).Select(r => r.CancelRequested).SingleAsync());
        return requested;
    }
}
