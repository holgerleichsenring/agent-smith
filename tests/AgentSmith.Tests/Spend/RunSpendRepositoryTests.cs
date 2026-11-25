using AgentSmith.Application.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Spend;

/// <summary>
/// 2026-11-24-0fbfb: the spend window over a real migrated store — every run whose id falls in it, of any pipeline,
/// in the caller's projects, with its repositories and the llm calls of exactly those runs; and the same window and
/// scope filtered in SQL on SQL Server and PostgreSQL.
/// </summary>
public sealed class RunSpendRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 11, 24, 23, 0, 0, TimeSpan.Zero);
    private static readonly string From = RunIdGenerator.TimestampOf(T);
    private static readonly string To = RunIdGenerator.TimestampOf(T.AddDays(1));
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunSpend_Window_ReadsEveryPipelinesRunsInsideIt()
    {
        var work = Seed("alpha", T.AddHours(1), "code", 10.65m, repos: ["api", "web"]);
        var system = Seed("alpha", T.AddHours(2), "pr-sweep", 2m);
        Seed("alpha", T.AddMinutes(-1), "code", 5m);
        Seed("alpha", T.AddDays(1), "code", 7m);
        SeedRaw("legacy-run", "alpha", "code", 9m);

        var read = await ReadAsync(ProjectScope.Everything);

        read.Runs.Select(r => (r.RunId, r.Pipeline, r.CostUsd)).Should().BeEquivalentTo(
            [(work, "code", 10.65m), (system, "pr-sweep", 2m)], "a run before the window, after it, or without a timestamp id is out");
        read.Runs.Single(r => r.RunId == work).Repos.Should().BeEquivalentTo(["api", "web"]);
    }

    [Fact]
    public async Task RunSpend_ScopedCaller_ReadsOnlyTheirProjects()
    {
        var alpha = Seed("alpha", T.AddHours(1), "code", 1m);
        Seed("beta", T.AddHours(1), "code", 4m);

        var read = await ReadAsync(ProjectScope.Of(["alpha"]));

        read.Runs.Select(r => r.RunId).Should().Equal(alpha);
    }

    [Fact]
    public async Task RunSpend_LlmCalls_CountsTheCallsOfTheWindowsRunsOnly()
    {
        Seed("alpha", T.AddHours(1), "code", 1m, calls: 3);
        Seed("alpha", T.AddMinutes(-5), "code", 1m, calls: 4);
        Seed("beta", T.AddHours(1), "code", 1m, calls: 5);

        (await ReadAsync(ProjectScope.Of(["alpha"]))).LlmCalls.Should().Be(3);
        (await ReadAsync(ProjectScope.Everything)).LlmCalls.Should().Be(8);
    }

    [Fact]
    public async Task RunSpend_EmptyWindow_ReadsNothing()
    {
        Seed("alpha", T.AddMinutes(-5), "code", 1m, calls: 2);

        var read = await ReadAsync(ProjectScope.Everything);

        read.Runs.Should().BeEmpty();
        read.LlmCalls.Should().Be(0);
    }

    [Fact]
    public void RunSpend_SqlServer_FiltersTheWindowAndScopeInTheQuery()
    {
        var options = new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlServer("Server=localhost;Database=x;TrustServerCertificate=True").Options;
        using var ctx = new AgentSmithDbContext(options);

        var sql = new RunSpendRepository(ctx).Window(ProjectScope.Of(["alpha"]), From, To).Select(r => r.Id).ToQueryString();

        sql.Should().Contain("[r].[Id] >= @").And.Contain("[r].[Id] < @").And.Contain("[r].[Project] = @");
    }

    [Fact]
    public void RunSpend_Postgres_FiltersTheWindowAndScopeInTheQuery()
    {
        var options = new DbContextOptionsBuilder<AgentSmithDbContext>().UseNpgsql("Host=localhost;Database=x").Options;
        using var ctx = new AgentSmithDbContext(options);

        var sql = new RunSpendRepository(ctx).Window(ProjectScope.Of(["alpha"]), From, To).Select(r => r.Id).ToQueryString();

        sql.Should().Contain("r.\"Id\" >= @").And.Contain("r.\"Id\" < @").And.Contain("r.\"Project\" = @");
    }

    private async Task<RunSpendRead> ReadAsync(ProjectScope scope)
    {
        await using var ctx = Context();
        return await new RunSpendRepository(ctx).ReadAsync(scope, From, To, CancellationToken.None);
    }

    private string Seed(string project, DateTimeOffset at, string pipeline, decimal cost, string[]? repos = null, int calls = 0)
    {
        var id = RunIdGenerator.Generate(at);
        SeedRaw(id, project, pipeline, cost, repos, calls);
        return id;
    }

    private void SeedRaw(string id, string project, string pipeline, decimal cost, string[]? repos = null, int calls = 0)
    {
        using var ctx = Context();
        ctx.Runs.Add(new Run { Id = id, Project = project, Pipeline = pipeline, TicketId = "T-1", Status = "success", StartedAt = T, CostTotalUsd = cost });
        foreach (var repo in repos ?? []) ctx.RunRepos.Add(new RunRepo { RunId = id, RepoName = repo });
        for (var i = 0; i < calls; i++) ctx.RunLlmCalls.Add(new RunLlmCall { RunId = id, Model = "m", CostUsd = 0.01m });
        ctx.SaveChanges();
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
