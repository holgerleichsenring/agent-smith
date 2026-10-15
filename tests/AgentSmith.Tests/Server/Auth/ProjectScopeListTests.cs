using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4dd: a caller bound to alpha reads the run list, its cursor page and the pull requests of alpha
/// alone — filtered in the query, so beta's busy history never crowds alpha's page out — and GET /api/projects
/// names alpha's projects. Every row id is unique: the server is shared by the class.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class ProjectScopeListTests(ProjectScopeAuthorityFixture fixture)
    : IClassFixture<ProjectScopeAuthorityFixture>
{
    private static readonly DateTimeOffset T = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunList_ScopedCaller_SeesOnlyTheirProjectsActiveAndRecent()
    {
        await fixture.SeedAsync(async db =>
        {
            await ProjectScopeRows.RunAsync(db, "r-list-alpha-live", "alpha");
            await ProjectScopeRows.RunAsync(db, "r-list-beta-live", "beta");
            await FinishedAsync(db, "r-list-alpha-done", "alpha", T);
            await FinishedAsync(db, "r-list-beta-done", "beta", T);
        });

        var (active, recent) = await RunsAsync(AlphaTeam, "/api/runs");
        var (adminActive, _) = await RunsAsync(Admins, "/api/runs");

        active.Should().Contain("r-list-alpha-live").And.NotContain("r-list-beta-live");
        recent.Should().Contain("r-list-alpha-done").And.NotContain("r-list-beta-done");
        adminActive.Should().Contain("r-list-beta-live", "a global holder reads every project as before");
    }

    [Fact]
    public async Task RunList_ScopedCaller_GetsAFullRecentPageWhileAnotherProjectIsBusy()
    {
        await fixture.SeedAsync(async db =>
        {
            await FinishedAsync(db, "r-busy-alpha", "alpha", T);
            for (var i = 0; i < 60; i++) await FinishedAsync(db, $"r-busy-zbeta-{i:00}", "beta", T.AddMinutes(i));
        });

        var (_, recent) = await RunsAsync(AlphaTeam, "/api/runs");

        recent.Should().Contain("r-busy-alpha", "beta's sixty newer runs are not the caller's page");
    }

    [Fact]
    public async Task RunList_CursorPage_ScopedCaller_SeesOnlyTheirProjects()
    {
        await fixture.SeedAsync(async db =>
        {
            await FinishedAsync(db, "r-page-alpha", "alpha", T.AddDays(-3));
            await FinishedAsync(db, "r-page-beta", "beta", T.AddDays(-3));
        });

        var (_, recent) = await RunsAsync(AlphaTeam, "/api/runs?before=2026-09-30T00:00:00Z&limit=200");

        recent.Should().Contain("r-page-alpha").And.NotContain("r-page-beta");
    }

    [Fact]
    public async Task PullRequests_ScopedCaller_SeesOnlyTheirProjectsPullRequests()
    {
        await fixture.SeedAsync(async db =>
        {
            await FinishedAsync(db, "r-pr-alpha", "alpha", T, "https://example.test/pr/1");
            await FinishedAsync(db, "r-pr-beta", "beta", T, "https://example.test/pr/2");
        });

        var response = await fixture.Server.Client.SendAsync(fixture.As(AlphaTeam, HttpMethod.Get, "/api/pull-requests"));
        var runIds = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .EnumerateArray().Select(row => row.GetProperty("runId").GetString()).ToList();

        runIds.Should().Contain("r-pr-alpha").And.NotContain("r-pr-beta");
    }

    [Fact]
    public async Task ProjectList_BootedScopedCaller_NamesTheBoundConfiguredProjects()
    {
        var response = await fixture.Server.Client.SendAsync(fixture.As(AlphaTeam, HttpMethod.Get, "/api/projects"));
        var admin = await fixture.Server.Client.SendAsync(fixture.As(Admins, HttpMethod.Get, "/api/projects"));

        (await Names(response)).Should().Equal("alpha", "team/alpha");
        (await Names(admin)).Should().Equal("alpha", "beta", "team/alpha");
    }

    private async Task<(List<string> Active, List<string> Recent)> RunsAsync(string group, string path)
    {
        var response = await fixture.Server.Client.SendAsync(fixture.As(group, HttpMethod.Get, path));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return (Ids(root, "active"), Ids(root, "recent"));
    }

    private static List<string> Ids(JsonElement root, string name) =>
        root.TryGetProperty(name, out var rows) ? [.. rows.EnumerateArray().Select(r => r.GetProperty("runId").GetString()!)] : [];

    private static async Task<List<string>> Names(HttpResponseMessage response) =>
        [.. JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.EnumerateArray().Select(n => n.GetString()!)];

    private static async Task FinishedAsync(AgentSmithDbContext db, string id, string project, DateTimeOffset at, string? pr = null)
    {
        db.Add(new Run
        {
            Id = id, Project = project, Pipeline = "code", TicketId = "T-9", Status = "success",
            StartedAt = at, FinishedAt = at.AddMinutes(1),
            PullRequestsJson = pr is null ? null : RunStoryJson.Serialize(new List<RunPullRequestView> { new("repo", "opened", pr, null, at) }),
        });
        await db.SaveChangesAsync();
    }
}
