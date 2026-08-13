using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0404: a finished run answers where its wall-clock went. The applier attributes
/// model time (and its throttle share) and sandbox time to the STEP that spent it;
/// the rail serves the four-way split per step and the run detail the roll-up.
/// Proven on a real SQLite engine, end to end from events to served view.
/// </summary>
public sealed class RunTimeSplitServedTests : IDisposable
{
    private const string RunId = "2026-08-13T09-00-00-0001";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-08-13T09:00:00Z");

    private readonly SqliteConnection _connection;
    private readonly IServiceScopeFactory _scopes;

    public RunTimeSplitServedTests()
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

    [Fact]
    public async Task StepTimeSplit_ModelAndSandboxAndScaffolding_SumToTheStepDuration()
    {
        await SeedTwoStepRunAsync();

        var steps = await new RunStepsReader(_scopes, new RunStepAggregatesReader(), new RunRailComposer())
            .ReadAsync(RunId, CancellationToken.None);

        var implement = steps.Single(s => s.StepIndex == 0);
        implement.Time.Should().NotBeNull();
        implement.Time!.ModelMs.Should().Be(1500);
        implement.Time.ThrottleMs.Should().Be(400, "the wait happened inside the measured call");
        implement.Time.SandboxMs.Should().Be(9050);
        // 20s step: 1.5s model + 9.05s sandbox leaves 9.45s of scaffolding.
        implement.Time.ScaffoldingMs.Should().Be(9450);
        (implement.Time.ModelMs + implement.Time.SandboxMs + implement.Time.ScaffoldingMs)
            .Should().Be(20_000, "the parts reconstruct the step's own wall-clock");
    }

    /// <summary>
    /// Serialisation is readable, not inferred: the step's own duration next to the
    /// SUMMED duration of the commands it ran says whether they ran one after another.
    /// The count comes from the SandboxCommand trail rows (p0388b), the sum from the
    /// SandboxResult attribution — so this projects both to prove they agree.
    /// </summary>
    [Fact]
    public async Task StepTimeSplit_SandboxCommandCountAndSummedDuration_ShowSerialisation()
    {
        var clock = new MutableTimeProvider { Now = T };
        var projector = new RunDbProjector(
            _scopes, new RunEventApplier(new(), new(), new(), new(), new(), new()), clock);
        foreach (var ev in SerialCommandRun()) await projector.ProjectAsync(ev, CancellationToken.None);
        clock.Now = clock.Now.AddSeconds(5);
        await projector.FlushStaleAsync(CancellationToken.None);

        var steps = await new RunStepsReader(_scopes, new RunStepAggregatesReader(), new RunRailComposer())
            .ReadAsync(RunId, CancellationToken.None);

        var implement = steps.Single(s => s.StepIndex == 0);
        implement.SandboxCommands.Should().Be(2);
        implement.Time!.SandboxMs.Should().Be(9050,
            "two commands summing to nearly half the step ran one after another");
    }

    private static IEnumerable<AgentSmith.Contracts.Events.RunEvent> SerialCommandRun() =>
    [
        new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
        new StepStartedEvent(RunId, 0, "Implement", 1, T),
        new SandboxCommandEvent(RunId, "primary", "ReadFile", 0, T) { OriginStepIndex = 0 },
        Sandbox(0, "ReadFile", durationMs: 50),
        new SandboxCommandEvent(RunId, "primary", "/bin/sh", 0, T) { OriginStepIndex = 0 },
        Sandbox(0, "/bin/sh", durationMs: 9000),
        new StepFinishedEvent(RunId, 0, "success", 20_000, T.AddSeconds(20)),
        new RunFinishedEvent(RunId, "success", null, "done", T.AddSeconds(21)),
    ];

    [Fact]
    public async Task StepTimeSplit_RunningStep_ReportsNoScaffolding()
    {
        await ApplyAsync(
            new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new StepStartedEvent(RunId, 0, "Implement", 2, T),
            Llm(0, durationMs: 700, throttleMs: 0));

        var steps = await new RunStepsReader(_scopes, new RunStepAggregatesReader(), new RunRailComposer())
            .ReadAsync(RunId, CancellationToken.None);

        steps.Single().Time!.ModelMs.Should().Be(700);
        steps.Single().Time!.ScaffoldingMs.Should().BeNull(
            "a step with no duration yet has no remainder to report");
    }

    [Fact]
    public void StepTimeSplit_MeasuredPartsExceedTheRoundedDuration_ScaffoldingClampsToZero()
    {
        var split = AgentSmith.Contracts.Runs.RunTimeSplitView.From(
            modelMs: 900, throttleMs: 0, sandboxMs: 200, durationSeconds: 1.0);

        split.ScaffoldingMs.Should().Be(0, "a negative remainder is rounding noise, not a finding");
    }

    [Fact]
    public async Task RunRollup_MatchesTheSumOfItsSteps()
    {
        await SeedTwoStepRunAsync();

        var run = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetRunDetailAsync(RunId, CancellationToken.None);
        var detail = RunSnapshotMapper.ToSnapshot(run!, includeStory: true);
        var steps = await new RunStepsReader(_scopes, new RunStepAggregatesReader(), new RunRailComposer())
            .ReadAsync(RunId, CancellationToken.None);

        detail.TimeSplit.Should().NotBeNull();
        detail.TimeSplit!.ModelMs.Should().Be(steps.Sum(s => s.Time!.ModelMs));
        detail.TimeSplit.ThrottleMs.Should().Be(steps.Sum(s => s.Time!.ThrottleMs));
        detail.TimeSplit.SandboxMs.Should().Be(steps.Sum(s => s.Time!.SandboxMs));
        detail.TimeSplit.ScaffoldingMs.Should().Be(steps.Sum(s => s.Time!.ScaffoldingMs ?? 0));
    }

    /// <summary>
    /// The defect this phase exists to remove: a FINISHED run reported 0ms of model
    /// time forever, because the fold lived only on the volatile broadcaster snapshot.
    /// </summary>
    [Fact]
    public async Task FinishedRun_ServedFromTheDatabase_KeepsItsModelAndThrottleTime()
    {
        await SeedTwoStepRunAsync();

        var run = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetRunDetailAsync(RunId, CancellationToken.None);
        var detail = RunSnapshotMapper.ToSnapshot(run!, includeStory: true);

        detail.Status.Should().Be("success");
        detail.LlmDurationMs.Should().Be(2300);
        detail.ThrottleWaitMs.Should().Be(400);
    }

    [Fact]
    public async Task RunWithoutAttributedTime_ServesNoSplit_NotZeros()
    {
        await ApplyAsync(
            new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T),
            new StepStartedEvent(RunId, 0, "Implement", 1, T),
            new StepFinishedEvent(RunId, 0, "success", 5_000, T.AddSeconds(5)),
            new RunFinishedEvent(RunId, "success", null, "done", T.AddSeconds(6)));

        var run = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetRunDetailAsync(RunId, CancellationToken.None);

        RunSnapshotMapper.ToSnapshot(run!, includeStory: true).TimeSplit.Should().BeNull();
    }

    [Fact]
    public async Task RunList_StaysLean_NoTimeSplit()
    {
        await SeedTwoStepRunAsync();

        var run = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetRunDetailAsync(RunId, CancellationToken.None);

        RunSnapshotMapper.ToSnapshot(run!).TimeSplit.Should().BeNull("the list stays lean");
    }

    // Step 0: 20s wall-clock, one 1.5s call (0.4s of it throttled), two commands
    // summing to 9.05s. Step 1: 4s wall-clock, one 0.8s call, no sandbox work.
    private Task SeedTwoStepRunAsync() => ApplyAsync(
        new RunStartedEvent(RunId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
        new StepStartedEvent(RunId, 0, "Implement", 2, T),
        Llm(0, durationMs: 1500, throttleMs: 400),
        Sandbox(0, "ReadFile", durationMs: 50),
        Sandbox(0, "/bin/sh", durationMs: 9000),
        new StepFinishedEvent(RunId, 0, "success", 20_000, T.AddSeconds(20)),
        new StepStartedEvent(RunId, 1, "Verify", 2, T.AddSeconds(20)),
        Llm(1, durationMs: 800, throttleMs: 0),
        new StepFinishedEvent(RunId, 1, "success", 4_000, T.AddSeconds(24)),
        new RunFinishedEvent(RunId, "success", null, "done", T.AddSeconds(25)));

    private static LlmCallFinishedEvent Llm(int stepIndex, long durationMs, long throttleMs) =>
        new(RunId, "m", "coder", 100, 10, 0.01m, durationMs, T, ThrottleWaitMs: throttleMs)
        { OriginStepIndex = stepIndex };

    private static SandboxResultEvent Sandbox(int stepIndex, string command, long durationMs) =>
        new(RunId, "primary", command, 0, durationMs, T, Summary: command)
        { OriginStepIndex = stepIndex };

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private async Task ApplyAsync(params AgentSmith.Contracts.Events.RunEvent[] events)
    {
        var applier = new RunEventApplier(new(), new(), new(), new(), new(), new());
        foreach (var ev in events)
        {
            await using var uow = new AgentSmithDbContext(Options());
            await applier.ApplyAsync(uow, ev, CancellationToken.None);
        }
    }
}
