using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0355: (a) a finished run's cost must be TRUE on revisit even when the run-end
/// event carried no cost — the DB total falls back to the sum of the persisted
/// per-call costs instead of a stale $0; (b) the runs list pages backwards by a
/// `before` timestamp cursor, newest-first, reaching runs beyond the recent window.
/// Proven on a real SQLite engine.
/// </summary>
public sealed class RunCostAndPaginationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RunCostAndPaginationTests()
    {
        _connection = MigratedStoreTemplate.OpenCopy();
    }

    public void Dispose() => _connection.Dispose();

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private RunDbProjector NewProjector()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AgentSmith.Infrastructure.Persistence.Services.RunTrailBuffers>();
        services.AddSingleton<RunDbProjector>();
        return services.BuildServiceProvider().GetRequiredService<RunDbProjector>();
    }

    private RunRepository NewStore() => new(new AgentSmithDbContext(Options()));

    private Task ProjectRunAsync(
        string runId, DateTimeOffset startedAt, decimal? finishedCost, params decimal[] llmCosts) =>
        ProjectPipelineRunAsync(runId, "code", startedAt, finishedCost, llmCosts);

    private async Task ProjectPipelineRunAsync(
        string runId, string pipeline, DateTimeOffset startedAt, decimal? finishedCost, params decimal[] llmCosts)
    {
        var projector = NewProjector();
        await projector.ProjectAsync(
            new RunStartedEvent(runId, "ticket", pipeline, new[] { "primary" }, startedAt, "claude", "42"),
            CancellationToken.None);
        foreach (var cost in llmCosts)
            await projector.ProjectAsync(
                new LlmCallFinishedEvent(runId, "gpt-4.1", "coding-agent", 1000, 200, cost, 1200, startedAt,
                    "implementation", "primary"),
                CancellationToken.None);
        await projector.ProjectAsync(
            new RunFinishedEvent(runId, "success", null, "done", startedAt, finishedCost),
            CancellationToken.None);
    }

    [Fact]
    public async Task CostPersistsAndReconstructsOnRevisit_WhenRunEndCostNull()
    {
        // Run-end event carries NO cost, but two LLM calls accumulated real cost.
        await ProjectRunAsync("run-1", DateTimeOffset.Parse("2026-06-07T12:00:00Z"),
            finishedCost: null, llmCosts: new[] { 0.05m, 0.07m });

        var run = await NewStore().GetRunDetailAsync("run-1", CancellationToken.None);

        run!.CostTotalUsd.Should().Be(0.12m, "the row falls back to the summed per-call cost, not $0");
    }

    [Fact]
    public async Task CostIsTheSumOfTheCalls_WhenTheRunEndCarriesAnotherTotal()
    {
        // 2026-11-17-166ab: a worker run's run-end total was 0 over $17.88 of recorded calls.
        await ProjectRunAsync("run-2", DateTimeOffset.Parse("2026-06-07T12:00:00Z"),
            finishedCost: 0m, llmCosts: new[] { 1.5m, 2.25m });

        var run = await NewStore().GetRunDetailAsync("run-2", CancellationToken.None);

        run!.CostTotalUsd.Should().Be(3.75m, "the run's cost is the sum of its recorded calls, whatever the tracker estimated");
    }

    [Fact]
    public async Task CostIsZero_WhenNoCallWasRecorded()
    {
        await ProjectRunAsync("run-3", DateTimeOffset.Parse("2026-06-07T12:00:00Z"), finishedCost: 0.99m);

        var run = await NewStore().GetRunDetailAsync("run-3", CancellationToken.None);

        run!.CostTotalUsd.Should().Be(0m);
    }

    [Fact]
    public async Task Projector_LlmCallFinished_AccumulatesLiveCost()
    {
        // p0355: a RUNNING run (no finish event yet) already shows the spend
        // made so far instead of $0.00 until finish.
        var projector = NewProjector();
        var t = DateTimeOffset.Parse("2026-06-07T12:00:00Z");
        await projector.ProjectAsync(
            new RunStartedEvent("run-live", "ticket", "code", new[] { "primary" }, t, "claude", "42"),
            CancellationToken.None);
        foreach (var cost in new[] { 0.05m, 0.07m })
            await projector.ProjectAsync(
                new LlmCallFinishedEvent("run-live", "gpt-4.1", "coding-agent", 1000, 200, cost, 1200, t,
                    "implementation", "primary"),
                CancellationToken.None);

        var run = await NewStore().GetRunDetailAsync("run-live", CancellationToken.None);

        run!.CostTotalUsd.Should().Be(0.12m, "a running run accumulates per-call cost live");
    }

    [Fact]
    public async Task Projector_LlmCallAfterFinish_AddsToTheTotal()
    {
        // 2026-11-17-166ab: the total is the sum of the rows, so a call that lands after the
        // finish (a draining sandbox, a lagging replica) still counts.
        var projector = NewProjector();
        var t = DateTimeOffset.Parse("2026-06-07T12:00:00Z");
        await ProjectRunAsync("run-late", t, finishedCost: 0.99m);
        await projector.ProjectAsync(
            new LlmCallFinishedEvent("run-late", "gpt-4.1", "coding-agent", 1000, 200, 0.05m, 1200, t,
                "implementation", "primary"),
            CancellationToken.None);

        var run = await NewStore().GetRunDetailAsync("run-late", CancellationToken.None);

        run!.CostTotalUsd.Should().Be(0.05m);
    }

    [Fact]
    public async Task RunsEndpoint_Pages_NewestFirst_BeforeCursor()
    {
        var t = DateTimeOffset.Parse("2026-06-07T12:00:00Z");
        await ProjectRunAsync("run-a", t, finishedCost: 0m);
        await ProjectRunAsync("run-b", t.AddMinutes(1), finishedCost: 0m);
        await ProjectRunAsync("run-c", t.AddMinutes(2), finishedCost: 0m);

        // Cursor between run-b and run-c → older runs are run-b, run-a, newest-first.
        var page = await NewStore().GetRunsBeforeAsync(ProjectScope.Everything, t.AddSeconds(90), limit: 10, CancellationToken.None);

        page.Select(r => r.Id).Should().Equal("run-b", "run-a");
    }

    [Fact]
    public async Task RunsEndpoint_Page_HonorsLimit()
    {
        var t = DateTimeOffset.Parse("2026-06-07T12:00:00Z");
        await ProjectRunAsync("run-a", t, finishedCost: 0m);
        await ProjectRunAsync("run-b", t.AddMinutes(1), finishedCost: 0m);
        await ProjectRunAsync("run-c", t.AddMinutes(2), finishedCost: 0m);

        var page = await NewStore().GetRunsBeforeAsync(ProjectScope.Everything, t.AddMinutes(5), limit: 1, CancellationToken.None);

        page.Select(r => r.Id).Should().Equal("run-c");
    }
    // 2026-11-24-6357: three work runs older than sixty system runs.
    private async Task<DateTimeOffset> SeedWorkUnderSystemBurstAsync()
    {
        var t = DateTimeOffset.Parse("2026-06-07T12:00:00Z");
        for (var i = 0; i < 3; i++)
            await ProjectPipelineRunAsync($"2026-06-07T12-00-0{i}-work", "code", t.AddSeconds(i), 0m);
        for (var i = 0; i < 60; i++)
            await ProjectPipelineRunAsync($"2026-06-07T13-{i:00}-00-beat", "series-run", t.AddHours(1).AddMinutes(i), 0m);
        return t;
    }

    [Fact]
    public async Task RecentWindow_OfWork_HoldsTheWorkRuns_UnderASystemBurst()
    {
        await SeedWorkUnderSystemBurstAsync();

        var work = await NewStore().GetRecentRunsAsync(ProjectScope.Everything, 50, CancellationToken.None, PipelineOrigin.Work);
        var system = await NewStore().GetRecentRunsAsync(ProjectScope.Everything, 50, CancellationToken.None, PipelineOrigin.System);
        var either = await NewStore().GetRecentRunsAsync(ProjectScope.Everything, 50, CancellationToken.None);

        work.Should().HaveCount(3).And.OnlyContain(r => r.Pipeline == "code");
        system.Should().HaveCount(50).And.OnlyContain(r => r.Pipeline == "series-run");
        either.Should().HaveCount(50).And.OnlyContain(r => r.Pipeline == "series-run");
    }

    [Fact]
    public async Task Page_OfWork_SkipsSystemRuns_AndEitherKeepsBoth()
    {
        var t = await SeedWorkUnderSystemBurstAsync();

        var work = await NewStore().GetRunsBeforeAsync(
            ProjectScope.Everything, t.AddHours(2), 20, CancellationToken.None, PipelineOrigin.Work);
        var either = await NewStore().GetRunsBeforeAsync(ProjectScope.Everything, t.AddHours(2), 20, CancellationToken.None);

        work.Select(r => r.Pipeline).Should().Equal("code", "code", "code");
        either.Should().HaveCount(20).And.OnlyContain(r => r.Pipeline == "series-run");
    }
}
