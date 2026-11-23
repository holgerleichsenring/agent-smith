using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>2026-11-23-31dcb: a series run belongs to its stored project. A caller bound to alpha lists and counts alpha's
/// series only and meets beta's series as the same empty 404 as one that does not exist.</summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class ProjectScopeSeriesRunTests(ProjectScopeAuthorityFixture fixture)
    : IClassFixture<ProjectScopeAuthorityFixture>
{
    [Fact]
    public async Task ProjectScope_SeriesRunList_HoldsTheCallersProjectsOnly()
    {
        await SeedAsync("sr-list-alpha", "alpha");
        await SeedAsync("sr-list-beta", "beta");

        var rows = await fixture.Server.Client.SendAsync(fixture.As(AlphaTeam, HttpMethod.Get, "/api/series-runs"));
        var count = await fixture.Server.Client.SendAsync(fixture.As(AlphaTeam, HttpMethod.Get, "/api/series-runs/count"));

        rows.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await rows.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToList();
        ids.Should().Contain("sr-list-alpha").And.NotContain("sr-list-beta");
        var running = (await count.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("running").GetInt32();
        running.Should().Be(ids.Count, "every series seeded here runs, and only the caller's are counted");
    }

    [Fact]
    public async Task ProjectScope_SeriesRunOfAnotherProject_IsTheSameEmpty404AsAnUnknownOne()
    {
        await SeedAsync("sr-detail-alpha", "alpha");
        await SeedAsync("sr-detail-beta", "beta");

        var own = await SendAsync(AlphaTeam, "/api/series-runs/sr-detail-alpha");
        var foreign = await SendAsync(AlphaTeam, "/api/series-runs/sr-detail-beta");
        var unknown = await SendAsync(AlphaTeam, "/api/series-runs/sr-none");

        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await own.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tickets").GetArrayLength().Should().Be(1);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await foreign.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SeriesRunCount_ASeriesOfAProjectNoLongerConfigured_IsNotCountedAsTheListDoesNotListIt()
    {
        await SeedAsync("sr-count-gone", "gone");

        var rows = await SendAsync(Admins, "/api/series-runs");
        var count = await SendAsync(Admins, "/api/series-runs/count");

        var ids = (await rows.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToList();
        ids.Should().NotContain("sr-count-gone");
        (await count.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("running").GetInt32().Should().Be(ids.Count);
    }

    [Fact]
    public async Task ProjectScope_GlobalHolder_ReadsASeriesRunOfEveryProject()
    {
        await SeedAsync("sr-admin-beta", "beta");

        (await SendAsync(Admins, "/api/series-runs/sr-admin-beta")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> SendAsync(string group, string path) =>
        fixture.Server.Client.SendAsync(fixture.As(group, HttpMethod.Get, path));

    private Task SeedAsync(string id, string project) => fixture.SeedAsync(async db =>
    {
        var record = new SeriesRunRecord(id, $"conv-{id}", project, "operator", true, SeriesRunStates.Running,
            [new SeriesTicket("T-1", "T-1", "Work", false, DateTimeOffset.UtcNow)], DateTimeOffset.UtcNow);
        db.Add(new SeriesRun
        {
            SeriesRunId = id, SessionId = record.Conversation, Project = project, State = record.State,
            ContentJson = JsonSerializer.Serialize(record),
        });
        await db.SaveChangesAsync();
    });
}
