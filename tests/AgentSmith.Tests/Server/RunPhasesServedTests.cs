using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0466: a finished phase is a record you can open. The phase is a ROW the producer
/// wrote, its steps and decisions name it in a column, and the spec it executed is held
/// by the server rather than only by the sandbox that is gone. Proven on a real SQLite
/// engine through the endpoint handlers.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class RunPhasesServedTests : IDisposable
{
    private const string RunId = "2026-08-19T09-00-00-0001";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-08-19T09:00:00Z");

    private readonly SqliteConnection _connection;
    private readonly IServiceScopeFactory _scopes;

    public RunPhasesServedTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options())) ctx.Database.Migrate();
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunArtifactRepository>();
        _scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunPhase_LifecycleEvents_ProduceOnePhaseRowPerSelectedPhase()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            Done("p19213a", "Make the thing exist"),
            Selected("p19213b", 2, "Make the thing readable"),
            Failed("p19213b", "Make the thing readable", "dotnet test exited 1"));

        var phases = await ReadPhasesAsync();

        phases.Select(p => p.PhaseId).Should().Equal("p19213a", "p19213b");
        phases.Select(p => p.Ordinal).Should().Equal(1, 2);
        phases[0].Status.Should().Be("done");
        phases[0].Title.Should().Be("Make the thing exist");
        phases[0].EndedAt.Should().NotBeNull();
        phases[1].Status.Should().Be("failed");
        phases[1].Verdict.Should().Be("dotnet test exited 1");
    }

    [Fact]
    public async Task RunPhase_SelectedTwice_StaysOneRow()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            Selected("p19213a", 1, "Make the thing exist"));

        (await ReadPhasesAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task RunStep_WrittenDuringAPhase_CarriesPhaseIdWithoutParsing()
    {
        await ApplyAsync(
            new StepStartedEvent(
                RunId, 4, "p19213a: Implement", 6, T, "p19213a: Implement",
                CommandNames.AgenticMaster, "p19213a"));

        await using var ctx = new AgentSmithDbContext(Options());
        ctx.RunSteps.Single().PhaseId.Should().Be("p19213a");
    }

    [Fact]
    public async Task RunDecision_LoggedDuringAPhase_CarriesPhaseId()
    {
        await ApplyAsync(new DecisionLoggedEvent(
            RunId, "tooling", "sqlite", "postgres", "smallest footprint", T, "p19213a"));

        await using var ctx = new AgentSmithDbContext(Options());
        ctx.RunDecisions.Single().PhaseId.Should().Be("p19213a");
    }

    /// <summary>
    /// The pre-migration row is what the regex exists for, and nothing backfills it: it
    /// must read exactly as it did before this phase.
    /// </summary>
    [Fact]
    public async Task RunStepsReader_PreMigrationRow_StillSplitsThePrefix()
    {
        await ApplyAsync(new StepStartedEvent(
            RunId, 0, "p19213a: Implement", 1, T, "p19213a: Implement", CommandNames.AgenticMaster));

        var rail = await ReadRailAsync();

        rail.Single().PhaseId.Should().Be("p19213a");
        rail.Single().StepName.Should().Be("Implement");
    }

    /// <summary>
    /// The COLUMN is the phase. A row whose label says one thing and whose column says
    /// another is served the column's answer — otherwise the parser is still deciding.
    /// </summary>
    [Fact]
    public async Task RunStepsReader_RowWithPhaseId_DoesNotConsultTheRegex()
    {
        await ApplyAsync(new StepStartedEvent(
            RunId, 0, "p00000z: Implement", 1, T, "p00000z: Implement",
            CommandNames.AgenticMaster, "p19213a"));

        (await ReadRailAsync()).Single().PhaseId.Should().Be("p19213a");
    }

    [Fact]
    public async Task PhaseRecord_AfterWrite_IsServedFromTheArtifactStore()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            new PhaseRecordedEvent(RunId, "p19213a", "phase: p19213a\ngoal: \"Make it exist\"\n", T));

        var detail = await ReadPhaseAsync("p19213a");

        detail!.Record.Should().Contain("goal: \"Make it exist\"");
        detail.Phase.PhaseId.Should().Be("p19213a");
    }

    [Fact]
    public async Task RunPhaseEndpoint_UnknownPhase_IsNotFound() =>
        (await RunPhaseQueryEndpoints.GetRunPhaseAsync(
            RunId, "p00000a", Phases(), CancellationToken.None))
            .Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();

    [Fact]
    public async Task RunPhasesEndpoint_FinishedRun_ReturnsEveryPhaseWithItsDecisions()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            new StepStartedEvent(
                RunId, 0, "p19213a: Implement", 2, T, "p19213a: Implement",
                CommandNames.AgenticMaster, "p19213a"),
            new DecisionLoggedEvent(
                RunId, "tooling", "sqlite", "postgres", "smallest footprint", T, "p19213a"),
            Done("p19213a", "Make the thing exist"),
            Selected("p19213b", 2, "Make the thing readable"),
            new DecisionLoggedEvent(
                RunId, "ui", "one panel", "two", "fewer places to look", T, "p19213b"),
            // A decision taken outside any phase belongs to none of them.
            new DecisionLoggedEvent(RunId, "run", "retry", null, "transient", T));

        var phases = await ReadPhasesAsync();

        phases.Should().HaveCount(2);
        phases[0].Decisions.Select(d => d.Name).Should().Equal("sqlite");
        phases[0].Steps.Select(s => s.StepName).Should().Equal("Implement");
        phases[1].Decisions.Select(d => d.Name).Should().Equal("one panel");
    }

    [Fact]
    public async Task RunPhasesEndpoint_RunWithoutPhases_ReturnsEmpty() =>
        (await ReadPhasesAsync()).Should().BeEmpty();

    private static PhaseStateChangedEvent Selected(string phaseId, int ordinal, string title) =>
        new(RunId, phaseId, ordinal, title, PhaseRunState.InProgress, null, T);

    private static PhaseStateChangedEvent Done(string phaseId, string title) =>
        new(RunId, phaseId, 1, title, PhaseRunState.Done, null, T.AddMinutes(5));

    private static PhaseStateChangedEvent Failed(string phaseId, string title, string verdict) =>
        new(RunId, phaseId, 2, title, PhaseRunState.Failed, verdict, T.AddMinutes(9));

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private RunStepsReader Steps() => new(_scopes, new RunStepAggregatesReader(), new RunRailComposer());
    private RunPhasesReader Phases() => new(_scopes, Steps());

    private async Task ApplyAsync(params AgentSmith.Contracts.Events.RunEvent[] events)
    {
        var applier = new RunEventApplier(
            new(), new(), new(), new(), new(), new(), new(), new(new()), new());
        foreach (var ev in events)
        {
            await using var ctx = new AgentSmithDbContext(Options());
            await applier.ApplyAsync(ctx, ev, CancellationToken.None);
        }
    }

    // The endpoint handlers ARE the surface under test — the same code path the
    // dashboard hits, minus the HTTP pipeline.
    private async Task<IReadOnlyList<RunPhaseView>> ReadPhasesAsync() =>
        ValueOf<IReadOnlyList<RunPhaseView>>(
            await RunPhaseQueryEndpoints.GetRunPhasesAsync(RunId, Phases(), CancellationToken.None),
            "phases");

    private async Task<RunPhaseDetailView?> ReadPhaseAsync(string phaseId)
    {
        var result = await RunPhaseQueryEndpoints.GetRunPhaseAsync(
            RunId, phaseId, Phases(), CancellationToken.None);
        return (result as IValueHttpResult)?.Value as RunPhaseDetailView;
    }

    private async Task<IReadOnlyList<RunStepView>> ReadRailAsync() =>
        ValueOf<IReadOnlyList<RunStepView>>(
            await RunStepQueryEndpoints.GetRunStepsAsync(RunId, Steps(), CancellationToken.None),
            "steps");

    private static T ValueOf<T>(IResult result, string property)
    {
        var payload = result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value;
        payload.Should().NotBeNull();
        var value = payload!.GetType().GetProperty(property)!.GetValue(payload);
        return value.Should().BeAssignableTo<T>().Subject;
    }
}
