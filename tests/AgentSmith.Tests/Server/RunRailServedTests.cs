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

    private RunStepsReader Steps() => new(_scopes, new RunStepAggregatesReader(), new RunRailComposer());
    private RunDecisionsReader Decisions() => new(_scopes);
    private TrailReader Trail() =>
        new(null!, _scopes, new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer());

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

    // p0395: a spliced phase step (p0393a) is projected with the phase id composed
    // into its names. The read path splits it back apart — PhaseId structured, the
    // names clean — for old prefixed rows exactly like new ones; an unspliced step
    // carries no phase.
    [Fact]
    public async Task StepsEndpoint_SplicedPhaseStep_ServesPhaseIdAndCleanNames()
    {
        await ProjectAsync(
            new RunStartedEvent(RunId, "ticket", "code", ["primary"], T, "claude", "42"),
            new StepStartedEvent(
                RunId, 0, "p19106a: Generating the plan", 2, T,
                "p19106a: Generate plan", CommandNames.FetchTicket),
            new StepFinishedEvent(RunId, 0, "success", 1000, T),
            new StepStartedEvent(RunId, 1, "Fetch ticket", 2, T, "Fetch ticket", CommandNames.FetchTicket));

        var rail = await ReadRailAsync();

        rail[0].PhaseId.Should().Be("p19106a");
        rail[0].StepName.Should().Be("Generating the plan");
        rail[0].DisplayName.Should().Be("Generate plan");
        rail[1].PhaseId.Should().BeNull();
        rail[1].DisplayName.Should().Be("Fetch ticket");
    }

    // p0398: the read path classifies every row (old records by command name) and
    // decides whether a gate has something to say — a gate whose summary is one
    // of its known no-op sentences carries hasFinding=false, so the drawer's
    // default view can drop it without the UI knowing any sentence.
    [Fact]
    public async Task RunStepsReader_Gate_NoOpSummary_HasFindingFalse()
    {
        await ProjectAsync(
            new RunStartedEvent(RunId, "ticket", "code", ["primary"], T, "claude", "42"),
            new StepStartedEvent(
                RunId, 0, "Hand the ticket back", 2, T, "Hand the ticket back", CommandNames.SpecHandback),
            new StepFinishedEvent(RunId, 0, "success", 100, T, "The derivation handed nothing back"),
            new StepStartedEvent(
                RunId, 1, "Fetch ticket", 2, T, "Fetch ticket", CommandNames.FetchTicket),
            new StepFinishedEvent(RunId, 1, "success", 100, T));

        var rail = await ReadRailAsync();

        rail[0].StepClass.Should().Be(CommandStepClasses.Gate);
        rail[0].HasFinding.Should().BeFalse();
        rail[1].StepClass.Should().Be(CommandStepClasses.Milestone);
    }

    [Fact]
    public async Task RunStepsReader_Gate_FailedOrParked_HasFindingTrue()
    {
        await ProjectAsync(
            new RunStartedEvent(RunId, "ticket", "code", ["primary"], T, "claude", "42"),
            new StepStartedEvent(
                RunId, 0, "Validate phase spec", 3, T, "Validate phase spec", CommandNames.PhaseSpecGate),
            new StepFinishedEvent(RunId, 0, "failed", 100, T, "Spec set carries no phase"),
            new StepStartedEvent(
                RunId, 1, "Hand the ticket back", 3, T, "Hand the ticket back", CommandNames.SpecHandback),
            new StepFinishedEvent(
                RunId, 1, "success", 100, T, "awaiting_user_input: handed back (contradiction)"),
            new StepStartedEvent(
                RunId, 2, "Set up private-feed credentials", 3, T,
                "Set up private-feed credentials", CommandNames.SetupRegistryAuth),
            new StepFinishedEvent(RunId, 2, "success", 100, T));

        var rail = await ReadRailAsync();

        rail[0].HasFinding.Should().BeTrue("a failed gate always speaks");
        rail[1].HasFinding.Should().BeTrue("a parked handback is a finding, not mechanics");
        rail[2].StepClass.Should().Be(CommandStepClasses.Internal);
        rail[2].HasFinding.Should().BeFalse("internals never carry the gate flag");
    }

    [Fact]
    public async Task StepEventsEndpoint_ClampsLimitAndReturnsCursor()
    {
        await SeedStepCommandsAsync(40);

        // Over the ceiling: clamped to MaxStepPageCount, so the page never ships
        // the whole trail even when the caller asks for it.
        var clamped = await ReadNewestPageAsync(0, limit: 100_000);
        clamped.Events.Count.Should().BeLessThanOrEqualTo(TrailReader.MaxStepPageCount);

        var first = await ReadNewestPageAsync(0, limit: 10);
        first.Events.Should().HaveCount(10);
        first.HasOlder.Should().BeTrue();
        first.OldestSeq.Should().BeGreaterThan(0);
        first.NewestSeq.Should().BeGreaterThan(first.OldestSeq);

        var older = await ReadOlderPageAsync(0, first.OldestSeq, limit: 10);
        older.Events.Should().HaveCount(10);
        older.NewestSeq.Should().BeLessThan(first.OldestSeq);
    }

    [Fact]
    public async Task StepEventsEndpoint_UnknownStepIndex_ReturnsEmptyPageNot500()
    {
        await ProjectAsync(
            new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new StepStartedEvent(RunId, 0, "Implement", 1, T));

        var page = await ReadNewestPageAsync(stepIndex: 99, limit: null);

        page.Events.Should().BeEmpty();
        page.HasOlder.Should().BeFalse();
        page.OldestSeq.Should().Be(0);
        page.NewestSeq.Should().Be(0);
    }

    // p0388d: the anchor. Opening a step that has produced thousands of rows has
    // to show its NEWEST ones — the p0388b query read ascending from Seq 0 and
    // handed back the step's FIRST page, which is the beginning of a step whose
    // interesting end is what the operator opened it for.
    [Fact]
    public async Task TrailReader_StepWithManyRows_ReturnsTheNewestPage()
    {
        await SeedStepCommandsAsync(40);

        var page = await ReadNewestPageAsync(0, limit: 10);

        Summaries(page.Events).Should().Equal(
            "cmd-30", "cmd-31", "cmd-32", "cmd-33", "cmd-34",
            "cmd-35", "cmd-36", "cmd-37", "cmd-38", "cmd-39");
        page.HasOlder.Should().BeTrue();
    }

    // p0388d: newest-anchored is not a substitute for history. Walking the
    // backwards cursor reaches the step's beginning, and the pages join without
    // a gap or a repeat — an operator diagnosing a run gets the whole step.
    [Fact]
    public async Task TrailReader_BackwardsCursor_WalksIntoHistoryWithoutGaps()
    {
        await SeedStepCommandsAsync(25);

        var page = await ReadNewestPageAsync(0, limit: 10);
        var walked = new List<string>(Summaries(page.Events));
        var pages = 1;
        while (page.HasOlder)
        {
            page = await ReadOlderPageAsync(0, page.OldestSeq, limit: 10);
            walked.InsertRange(0, Summaries(page.Events));
            pages++;
        }

        pages.Should().Be(3);
        walked.Should().Equal(Enumerable.Range(0, 25).Select(i => $"cmd-{i}"));
    }

    // p0388d: nothing re-read, so a step that was still producing rows looked
    // finished. The forward delta is what the open step polls while the run is
    // live — only what is new, in display order.
    [Fact]
    public async Task TrailReader_ForwardDelta_ShipsOnlyTheRowsAfterTheCursor()
    {
        await SeedStepCommandsAsync(25);

        var opened = await ReadNewestPageAsync(0, limit: 10);
        var idle = await ReadDeltaAsync(0, opened.NewestSeq, limit: 10);
        idle.Events.Should().BeEmpty("nothing has been written since the page was read");
        idle.NewestSeq.Should().Be(opened.NewestSeq);

        await SeedStepCommandsAsync(3, startAt: 25);
        var delta = await ReadDeltaAsync(0, opened.NewestSeq, limit: 10);

        Summaries(delta.Events).Should().Equal("cmd-25", "cmd-26", "cmd-27");
        delta.HasNewer.Should().BeFalse();
        delta.NewestSeq.Should().BeGreaterThan(opened.NewestSeq);
    }

    // p0388d: a delta larger than one page says so, so a caller catching up on a
    // busy step reads on immediately instead of trailing it a page per tick.
    [Fact]
    public async Task TrailReader_ForwardDeltaLargerThanAPage_SaysMoreIsWaiting()
    {
        await SeedStepCommandsAsync(30);

        var delta = await ReadDeltaAsync(0, sinceSeq: 1, limit: 10);

        delta.Events.Should().HaveCount(10);
        delta.HasNewer.Should().BeTrue();
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

    private SandboxCommandEvent SandboxCommand(int stepIndex, string summary = "build") =>
        new(RunId, "primary", "run_command", 12, T, summary) { OriginStepIndex = stepIndex };

    /// <summary>
    /// p0388d: a step that has produced <paramref name="count"/> individually
    /// identifiable rows, so a page can be checked for WHICH end of the step it
    /// came from. Called again with <paramref name="startAt"/> set, it extends
    /// the same step the way a live run does.
    /// </summary>
    private async Task SeedStepCommandsAsync(int count, int startAt = 0)
    {
        var events = new List<AgentSmith.Contracts.Events.RunEvent>();
        if (startAt == 0)
        {
            events.Add(new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"));
            events.Add(new StepStartedEvent(RunId, 0, "Implement", 1, T));
        }
        for (var i = startAt; i < startAt + count; i++) events.Add(SandboxCommand(0, $"cmd-{i}"));
        await ProjectAsync([.. events]);
    }

    // One projector across the whole test, because Seq is assigned by its trail
    // buffer: a fresh instance would restart the run's sequence at zero.
    private readonly MutableTimeProvider _clock = new() { Now = T };
    private RunDbProjector? _projector;

    private async Task ProjectAsync(params AgentSmith.Contracts.Events.RunEvent[] events)
    {
        _projector ??= new RunDbProjector(_scopes, new RunEventApplier(new(), new(), new(), new(), new(), new(), new()), _clock);
        foreach (var ev in events) await _projector.ProjectAsync(ev, CancellationToken.None);
        // A run without a terminal event keeps a partial trail buffer; age it past
        // the flush window so the rows the queries read actually exist.
        _clock.Now = _clock.Now.AddSeconds(5);
        await _projector.FlushStaleAsync(CancellationToken.None);
    }

    private static List<string> Summaries(IEnumerable<object> events) =>
        events.Cast<SandboxCommandEvent>().Select(e => e.Summary ?? "").ToList();

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

    // p0388d: no cursor means "the newest page" — what opening a step returns.
    private Task<StepPageShape> ReadNewestPageAsync(int stepIndex, int? limit) =>
        ReadStepPageAsync(stepIndex, beforeSeq: null, limit);

    private Task<StepPageShape> ReadOlderPageAsync(int stepIndex, long beforeSeq, int? limit) =>
        ReadStepPageAsync(stepIndex, beforeSeq, limit);

    private async Task<StepPageShape> ReadStepPageAsync(int stepIndex, long? beforeSeq, int? limit)
    {
        var result = await RunStepQueryEndpoints.GetRunStepEventsAsync(
            RunId, stepIndex, sinceSeq: null, beforeSeq, limit, Trail(), CancellationToken.None);
        return new StepPageShape(
            ValueOf<IReadOnlyList<object>>(result, "events"),
            ValueOf<long>(result, "oldestSeq"),
            ValueOf<long>(result, "newestSeq"),
            ValueOf<bool>(result, "hasOlder"));
    }

    private async Task<StepDeltaShape> ReadDeltaAsync(int stepIndex, long sinceSeq, int? limit)
    {
        var result = await RunStepQueryEndpoints.GetRunStepEventsAsync(
            RunId, stepIndex, sinceSeq, beforeSeq: null, limit, Trail(), CancellationToken.None);
        return new StepDeltaShape(
            ValueOf<IReadOnlyList<object>>(result, "events"),
            ValueOf<long>(result, "newestSeq"),
            ValueOf<bool>(result, "hasNewer"));
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

    private sealed record StepPageShape(
        IReadOnlyList<object> Events, long OldestSeq, long NewestSeq, bool HasOlder);

    private sealed record StepDeltaShape(
        IReadOnlyList<object> Events, long NewestSeq, bool HasNewer);
}
