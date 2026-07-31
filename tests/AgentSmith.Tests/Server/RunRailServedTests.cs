using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0388b: the run detail's full pipeline is served from the DB projections as
/// bounded queries — the rail from RunStep with per-step aggregates over the
/// p0388a-attributed child rows, a step's body as one clamped page, and the
/// latest decisions from RunDecision. No response ships the whole trail.
/// Proven on a real SQLite engine through the endpoint handlers.
/// </summary>
public sealed class RunRailServedTests : IDisposable
{
    private const string RunId = "2026-07-29T09-00-00-0001";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-07-29T09:00:00Z");

    private readonly SqliteConnection _connection;
    private readonly IServiceScopeFactory _scopes;

    public RunRailServedTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options())) ctx.Database.Migrate();
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        _scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose() => _connection.Dispose();

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private RunStepsReader Steps() => new(_scopes);
    private RunDecisionsReader Decisions() => new(_scopes);
    private TrailReader Trail() => new(null!, _scopes);

    [Fact]
    public async Task StepsEndpoint_RunningRun_ReturnsEveryStepInIndexOrderWithStatus()
    {
        await ProjectAsync(
            new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new StepStartedEvent(RunId, 0, "Fetch ticket", 3, T, "Fetch ticket", CommandNames.FetchTicket),
            new StepFinishedEvent(RunId, 0, "success", 1000, T),
            new StepStartedEvent(RunId, 1, "Analyze codebase", 3, T, "Analyze codebase", CommandNames.AnalyzeCode),
            new StepFinishedEvent(RunId, 1, "failed", 2000, T, "analyzer gave up"),
            // Still running — no StepFinished for index 2.
            new StepStartedEvent(RunId, 2, "Implement", 3, T, "Implement", CommandNames.SkillRound));

        var rail = await ReadRailAsync();

        rail.Select(s => s.StepIndex).Should().Equal(0, 1, 2);
        rail.Select(s => s.Status).Should().Equal("success", "failed", "running");
        rail[0].DisplayName.Should().Be("Fetch ticket");
        rail[1].ResultMessage.Should().Be("analyzer gave up");
        rail[1].DurationSeconds.Should().Be(2.0);
    }

    [Fact]
    public async Task StepsEndpoint_PerStepAggregates_MatchTheAttributedChildRows()
    {
        await ProjectAsync(
            new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new StepStartedEvent(RunId, 0, "Analyze codebase", 2, T),
            LlmCall(0, 0.25m), LlmCall(0, 0.75m),
            SandboxCommand(0), SandboxCommand(0), SandboxCommand(0),
            new SubAgentSpawnedEvent(RunId, "sub-1", "scout", "scanning", null, "h", T) { OriginStepIndex = 0 },
            new StepFinishedEvent(RunId, 0, "success", 500, T),
            new StepStartedEvent(RunId, 1, "Implement", 2, T),
            LlmCall(1, 2m),
            // Unattributed (pre-p0388a shape): counted for no step, never guessed.
            new LlmCallFinishedEvent(RunId, "claude", "master", 10, 2, 9m, 5, T));

        var rail = await ReadRailAsync();

        rail[0].LlmCalls.Should().Be(2);
        rail[0].CostUsd.Should().Be(1.0m);
        rail[0].SandboxCommands.Should().Be(3);
        rail[0].SubAgents.Should().Be(1);
        rail[1].LlmCalls.Should().Be(1);
        rail[1].CostUsd.Should().Be(2m);
        rail[1].SandboxCommands.Should().Be(0);
        rail[1].SubAgents.Should().Be(0);
    }

    [Fact]
    public async Task StepEventsEndpoint_ClampsLimitAndReturnsCursor()
    {
        var events = new List<AgentSmith.Contracts.Events.RunEvent>
        {
            new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new StepStartedEvent(RunId, 0, "Implement", 1, T),
        };
        for (var i = 0; i < 40; i++) events.Add(SandboxCommand(0));
        await ProjectAsync([.. events]);

        // Over the ceiling: clamped to MaxStepPageCount, so the page never ships
        // the whole trail even when the caller asks for it.
        var clamped = await ReadStepPageAsync(0, sinceSeq: 0, limit: 100_000);
        clamped.Events.Count.Should().BeLessThanOrEqualTo(TrailReader.MaxStepPageCount);

        var first = await ReadStepPageAsync(0, sinceSeq: 0, limit: 10);
        first.Events.Should().HaveCount(10);
        first.HasMore.Should().BeTrue();
        first.NextSeq.Should().BeGreaterThan(0);

        var second = await ReadStepPageAsync(0, first.NextSeq, limit: 10);
        second.Events.Should().HaveCount(10);
        second.NextSeq.Should().BeGreaterThan(first.NextSeq);
    }

    [Fact]
    public async Task StepEventsEndpoint_UnknownStepIndex_ReturnsEmptyPageNot500()
    {
        await ProjectAsync(
            new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new StepStartedEvent(RunId, 0, "Implement", 1, T));

        var page = await ReadStepPageAsync(stepIndex: 99, sinceSeq: 0, limit: null);

        page.Events.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
        page.NextSeq.Should().Be(0);
    }

    [Fact]
    public async Task DecisionsEndpoint_ReturnsLatestFirst()
    {
        await ProjectAsync(
            new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new DecisionLoggedEvent(RunId, "tooling", "first", null, "because", T) { OriginStepIndex = 0 },
            new DecisionLoggedEvent(RunId, "tooling", "second", null, "because", T) { OriginStepIndex = 1 },
            new DecisionLoggedEvent(RunId, "tooling", "third", null, "because", T) { OriginStepIndex = 2 });

        var decisions = await ReadDecisionsAsync(limit: 2);

        decisions.Select(d => d.Name).Should().Equal("third", "second");
        decisions[0].StepIndex.Should().Be(2);
    }

    // p0388c: the notes render "decision · <category> · <time>", so the endpoint
    // has to carry the category the producer classified the decision with.
    [Fact]
    public async Task DecisionsEndpoint_ReturnsTheProducersCategory()
    {
        await ProjectAsync(
            new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new DecisionLoggedEvent(RunId, "persistence", "sqlite", "postgres", "footprint", T));

        var decisions = await ReadDecisionsAsync(limit: 5);

        decisions.Single().Category.Should().Be("persistence");
    }

    private LlmCallFinishedEvent LlmCall(int stepIndex, decimal cost) =>
        new(RunId, "claude", "coding-agent", 100, 20, cost, 500, T) { OriginStepIndex = stepIndex };

    private SandboxCommandEvent SandboxCommand(int stepIndex) =>
        new(RunId, "primary", "run_command", 12, T, "build") { OriginStepIndex = stepIndex };

    private async Task ProjectAsync(params AgentSmith.Contracts.Events.RunEvent[] events)
    {
        var clock = new MutableTimeProvider { Now = T };
        var projector = new RunDbProjector(_scopes, new RunEventApplier(), clock);
        foreach (var ev in events) await projector.ProjectAsync(ev, CancellationToken.None);
        // A run without a terminal event keeps a partial trail buffer; age it past
        // the flush window so the rows the queries read actually exist.
        clock.Now = T.AddSeconds(5);
        await projector.FlushStaleAsync(CancellationToken.None);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // The endpoint handlers ARE the surface under test: the same code path the
    // dashboard hits, minus the HTTP pipeline.
    private async Task<IReadOnlyList<RunStepView>> ReadRailAsync() =>
        ValueOf<IReadOnlyList<RunStepView>>(
            await RunStepQueryEndpoints.GetRunStepsAsync(RunId, Steps(), CancellationToken.None),
            "steps");

    private async Task<StepPageShape> ReadStepPageAsync(int stepIndex, long sinceSeq, int? limit)
    {
        var result = await RunStepQueryEndpoints.GetRunStepEventsAsync(
            RunId, stepIndex, sinceSeq, limit, Trail(), CancellationToken.None);
        return new StepPageShape(
            ValueOf<IReadOnlyList<object>>(result, "events"),
            ValueOf<long>(result, "nextSeq"),
            ValueOf<bool>(result, "hasMore"));
    }

    private async Task<IReadOnlyList<RunDecisionView>> ReadDecisionsAsync(int limit) =>
        ValueOf<IReadOnlyList<RunDecisionView>>(
            await RunStepQueryEndpoints.GetRunDecisionsAsync(
                RunId, limit, Decisions(), CancellationToken.None),
            "decisions");

    private static T ValueOf<T>(IResult result, string property)
    {
        var payload = result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value;
        payload.Should().NotBeNull();
        var value = payload!.GetType().GetProperty(property)!.GetValue(payload);
        return value.Should().BeAssignableTo<T>().Subject;
    }

    private sealed record StepPageShape(IReadOnlyList<object> Events, long NextSeq, bool HasMore);
}
