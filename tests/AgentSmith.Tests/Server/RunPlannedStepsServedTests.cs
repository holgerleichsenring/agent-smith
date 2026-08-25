using AgentSmith.Tests.TestSupport;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0405: the rail is ONE ordered sequence — executed steps followed by the ones
/// the executor announced and has not reached — so a run in flight answers "what
/// is still coming" and not only "how many are left". The client consumes it; all
/// the sequence logic is here.
/// </summary>
public sealed class RunPlannedStepsServedTests : IDisposable
{
    private const string RunId = "2026-08-13T11-00-00-0001";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-08-13T11:00:00Z");

    private readonly SqliteConnection _connection;
    private readonly IServiceScopeFactory _scopes;

    public RunPlannedStepsServedTests()
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
    public async Task RunSteps_MidRun_ReturnsExecutedThenPlanned_SummingToTotalSteps()
    {
        await SeedMidRunAsync();

        var rail = await ReadRailAsync();

        rail.Should().HaveCount(TotalSteps, "executed plus planned IS the run's step total");
        rail.Select(s => s.StepIndex).Should().BeInAscendingOrder();
        rail.Take(2).Should().OnlyContain(s => !s.Planned);
        rail.Skip(2).Should().OnlyContain(s => s.Planned);
        rail[2].DisplayName.Should().Be(CommandDisplayNames.Get(CommandNames.AgenticMaster));
        rail[2].PhaseId.Should().Be("p19106a", "planned rows are grouped by phase like executed ones");
        rail.Last().PhaseId.Should().Be("p19106b");
    }

    [Fact]
    public async Task PlannedStep_CarriesNoStatusCostOrDuration()
    {
        await SeedMidRunAsync();

        var planned = (await ReadRailAsync()).First(s => s.Planned);

        planned.Status.Should().BeNull();
        planned.DurationSeconds.Should().BeNull();
        planned.CostUsd.Should().BeNull();
        planned.LlmCalls.Should().BeNull();
        planned.SandboxCommands.Should().BeNull();
        planned.SubAgents.Should().BeNull();
        planned.ResultMessage.Should().BeNull();
        planned.Time.Should().BeNull();
    }

    [Fact]
    public async Task PlannedStep_CarriesItsCommandsDisplayClass_SoTheDrawerCondensesTheFutureToo()
    {
        await SeedMidRunAsync();

        var rail = await ReadRailAsync();

        rail.First(s => s.CommandName == CommandNames.VerifyPhase && s.Planned)
            .StepClass.Should().Be(CommandStepClasses.Get(CommandNames.VerifyPhase));
    }

    [Fact]
    public async Task FinishedRun_HasNothingStillComing()
    {
        await SeedMidRunAsync();
        await ApplyAsync(new RunFinishedEvent(RunId, "success", null, "done", T.AddMinutes(5)));

        (await ReadRailAsync()).Should().OnlyContain(s => !s.Planned,
            "a run that has stopped has no future to show");
    }

    [Fact]
    public async Task RunWithoutAnAnnouncement_ServesOnlyWhatRan()
    {
        await ApplyAsync(
            new RunStartedEvent(RunId, "ticket", "code", ["primary"], T, "claude", "42"),
            new StepStartedEvent(RunId, 1, "Fetch ticket", 2, T, "Fetch ticket", CommandNames.FetchTicket));

        var rail = await ReadRailAsync();

        rail.Should().ContainSingle().Which.Planned.Should().BeFalse(
            "a pre-p0405 run has no announcement, and a preset is not a substitute for one");
    }

    /// <summary>
    /// A splice replaces the announcement wholesale: the phase block the executor
    /// added is in the new one, and the old one is not partially valid.
    /// </summary>
    [Fact]
    public async Task LaterAnnouncement_ReplacesTheEarlierOne()
    {
        await ApplyAsync(
            new RunStartedEvent(RunId, "ticket", "code", ["primary"], T, "claude", "42"),
            Announce(1, [Planned(1, CommandNames.FetchTicket, null), Planned(2, CommandNames.PhaseSequence, null)]),
            new StepStartedEvent(RunId, 1, "Fetch ticket", 2, T, "Fetch ticket", CommandNames.FetchTicket),
            new StepFinishedEvent(RunId, 1, "success", 1000, T),
            Announce(1, [
                Planned(1, CommandNames.FetchTicket, null),
                Planned(2, CommandNames.PhaseSequence, null),
                Planned(3, CommandNames.AgenticMaster, "p1"),
            ]));

        var rail = await ReadRailAsync();

        rail.Should().HaveCount(3);
        rail.Where(s => s.Planned).Select(s => s.StepIndex).Should().Equal(2, 3);
    }

    // Two of eight steps have run: the ticket was fetched and the sequence spliced
    // two phases, whose blocks are the six steps still to come.
    private const int TotalSteps = 8;

    private Task SeedMidRunAsync() => ApplyAsync(
        new RunStartedEvent(RunId, "ticket", "code", ["primary"], T, "claude", "42"),
        new StepStartedEvent(RunId, 1, "Fetch ticket", 2, T, "Fetch ticket", CommandNames.FetchTicket),
        new StepFinishedEvent(RunId, 1, "success", 1000, T),
        new StepStartedEvent(RunId, 2, "Work through the phases", 2, T, "Work through the phases",
            CommandNames.PhaseSequence),
        new StepFinishedEvent(RunId, 2, "success", 500, T),
        Announce(1, [
            Planned(1, CommandNames.FetchTicket, null),
            Planned(2, CommandNames.PhaseSequence, null),
            Planned(3, CommandNames.AgenticMaster, "p19106a"),
            Planned(4, CommandNames.VerifyPhase, "p19106a"),
            Planned(5, CommandNames.WritePhaseRecord, "p19106a"),
            Planned(6, CommandNames.AgenticMaster, "p19106b"),
            Planned(7, CommandNames.VerifyPhase, "p19106b"),
            Planned(8, CommandNames.WritePhaseRecord, "p19106b"),
        ]));

    private static PlannedStepView Planned(int index, string command, string? phaseId) =>
        new(index, command, CommandDisplayNames.Get(command), phaseId);

    private static PipelineStepsPlannedEvent Announce(int firstStepIndex, PlannedStepView[] steps) =>
        new(RunId, firstStepIndex, RunStoryJson.Serialize(steps), T);

    private Task<IReadOnlyList<RunStepView>> ReadRailAsync() =>
        new RunStepsReader(_scopes, new RunStepAggregatesReader(), new RunRailComposer())
            .ReadAsync(RunId, CancellationToken.None);

    private async Task ApplyAsync(params AgentSmith.Contracts.Events.RunEvent[] events)
    {
        var applier = RunEventAppliers.Default();
        foreach (var ev in events)
        {
            await using var uow = new AgentSmithDbContext(Options());
            await applier.ApplyAsync(uow, ev, CancellationToken.None);
        }
    }
}
