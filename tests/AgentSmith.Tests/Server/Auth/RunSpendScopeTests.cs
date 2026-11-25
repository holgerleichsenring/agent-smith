using System.Net;
using System.Text.Json;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-11-24-0fbfb: GET /api/runs/spend for a caller bound to alpha counts alpha's runs alone, work and system apart,
/// and books them by repositories and pipeline; a request naming no instant is refused. The server is shared by the
/// class, so each case reads the difference its own seeds made.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class RunSpendScopeTests(ProjectScopeAuthorityFixture fixture) : IClassFixture<ProjectScopeAuthorityFixture>
{
    [Fact]
    public async Task RunSpend_ScopedCaller_CountsItsProjectsWorkAndSystemApart()
    {
        var path = $"/api/runs/spend?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-2).ToString("O"))}";
        var before = await ReadAsync(AlphaTeam, path);
        var at = DateTimeOffset.UtcNow.AddMinutes(-30);
        await fixture.SeedAsync(async db =>
        {
            Add(db, "alpha", "code", 1.5m, at, "spend-repo");
            Add(db, "alpha", PipelinePresets.SeriesRunName, 0.5m, at);
            Add(db, "beta", "code", 7m, at);
            Add(db, "alpha", "code", 9m, DateTimeOffset.UtcNow.AddHours(-3));
            await db.SaveChangesAsync();
        });

        var after = await ReadAsync(AlphaTeam, path);

        Delta(after, before, "totalUsd").Should().Be(2m);
        Delta(after.GetProperty("work"), before.GetProperty("work"), "costUsd").Should().Be(1.5m);
        Delta(after.GetProperty("system"), before.GetProperty("system"), "costUsd").Should().Be(0.5m);
        (after.GetProperty("system").GetProperty("runs").GetInt32() - before.GetProperty("system").GetProperty("runs").GetInt32())
            .Should().Be(1);
        after.GetProperty("slices").EnumerateArray()
            .Should().Contain(s => s.GetProperty("repos").EnumerateArray().Any(r => r.GetString() == "spend-repo")
                                   && s.GetProperty("pipeline").GetString() == "code" && s.GetProperty("costUsd").GetDecimal() == 1.5m);
    }

    [Fact]
    public async Task RunSpend_NoInstant_Is400()
    {
        var response = await fixture.Server.Client.SendAsync(fixture.As(Admins, HttpMethod.Get, "/api/runs/spend"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static void Add(AgentSmithDbContext db, string project, string pipeline, decimal cost, DateTimeOffset at, string? repo = null)
    {
        var id = RunIdGenerator.Generate(at);
        db.Add(new Run { Id = id, Project = project, Pipeline = pipeline, TicketId = "S-1", Status = "success", StartedAt = at, CostTotalUsd = cost });
        if (repo is not null) db.Add(new RunRepo { RunId = id, RepoName = repo });
    }

    private static decimal Delta(JsonElement after, JsonElement before, string name) =>
        after.GetProperty(name).GetDecimal() - before.GetProperty(name).GetDecimal();

    private async Task<JsonElement> ReadAsync(string group, string path)
    {
        var response = await fixture.Server.Client.SendAsync(fixture.As(group, HttpMethod.Get, path));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }
}
