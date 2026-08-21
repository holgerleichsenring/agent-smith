using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Init;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0489: the operator's manual init launch. It is ticketless by construction —
/// no claim, no lease, no tracker call — and the run rows are its double-start
/// guard. Run rows and the capacity ledger are REAL here (in-memory SQLite over
/// the production repositories); only the sandbox probe and the job queue are
/// doubles, because that is where the process boundary actually is.
/// </summary>
public sealed class InitRunLauncherTests : IDisposable
{
    private const string Project = "sample";
    private const string InitPipeline = "init-project";
    // p0490: these cases are about admission, not about what happens to the pull
    // requests afterwards — the flag's own journey has its own test below.
    private const bool AutoComplete = false;

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly List<PipelineRequest> _enqueued = [];
    private readonly Mock<ITicketProviderFactory> _tickets = new(MockBehavior.Strict);
    private readonly Mock<ITicketClaimService> _claims = new(MockBehavior.Strict);
    private ISandboxCapacityProbe _probe = AdmittingProbe();
    private string? _budgetMemory;

    public InitRunLauncherTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options())) ctx.Database.Migrate();

        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunCapacityRepository>();
        services.AddSingleton(TimeProvider.System);
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task InitLauncher_TicketlessRequest_IsEnqueued_AndNoTrackerCallIsMade()
    {
        var result = await NewLauncher().LaunchAsync(Project, AutoComplete, CancellationToken.None);

        result.Outcome.Should().Be(InitLaunchOutcome.Started);
        var request = _enqueued.Should().ContainSingle().Subject;
        request.PipelineName.Should().Be(InitPipeline);
        request.TicketId.Should().BeNull("a manual init has no ticket and fabricates none");
        request.IsInit.Should().BeTrue();
        request.Headless.Should().BeTrue();
        request.RunId.Should().Be(result.RunId);
        _tickets.VerifyNoOtherCalls();
        _claims.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InitLauncher_TheRunRow_CarriesTriggerManual_AndNoTicketId()
    {
        var result = await NewLauncher().LaunchAsync(Project, AutoComplete, CancellationToken.None);

        using var ctx = new AgentSmithDbContext(Options());
        var run = ctx.Runs.Single(r => r.Id == result.RunId);
        run.Trigger.Should().Be("manual");
        run.TicketId.Should().BeEmpty();
        run.Project.Should().Be(Project);
        run.Pipeline.Should().Be(InitPipeline);
        run.Status.Should().Be("queued");
        run.FinishedAt.Should().BeNull();
    }

    [Fact]
    public async Task InitLauncher_SecondLaunchWhileOneRuns_IsRefused_WithTheLiveRunId()
    {
        var first = await NewLauncher().LaunchAsync(Project, AutoComplete, CancellationToken.None);

        var second = await NewLauncher().LaunchAsync(Project, AutoComplete, CancellationToken.None);

        second.Outcome.Should().Be(InitLaunchOutcome.AlreadyRunning);
        second.RunId.Should().Be(first.RunId, "the answer is the run that is already going");
        _enqueued.Should().ContainSingle("a second click must not start a second init");
    }

    [Fact]
    public async Task InitLauncher_BudgetDoesNotFit_IsRefused_WithTheBudgetsReason_AndNothingIsEnqueued()
    {
        _budgetMemory = "1Mi"; // the stub footprint is 4Gi — it cannot fit

        var result = await NewLauncher().LaunchAsync(Project, AutoComplete, CancellationToken.None);

        result.Outcome.Should().Be(InitLaunchOutcome.NoCapacity);
        result.Reason.Should().Contain("exceeds the remaining budget");
        result.RunId.Should().BeNull();
        _enqueued.Should().BeEmpty();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Should().BeEmpty("a refused launch writes no run row either");
    }

    [Fact]
    public async Task InitLauncher_UnknownProject_IsRefused()
    {
        var result = await NewLauncher().LaunchAsync("not-configured", AutoComplete, CancellationToken.None);

        result.Outcome.Should().Be(InitLaunchOutcome.UnknownProject);
        result.Reason.Should().Contain("not-configured");
        _enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task InitLauncher_RefusedLaunch_ReservesNothing()
    {
        _probe = DenyingProbe("namespace quota is full");

        var result = await NewLauncher().LaunchAsync(Project, AutoComplete, CancellationToken.None);

        result.Outcome.Should().Be(InitLaunchOutcome.NoCapacity);
        result.Reason.Should().Be("namespace quota is full");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Set<AgentSmith.Infrastructure.Persistence.Entities.RunCapacity>()
            .Should().BeEmpty("a refused launch leaves no footprint and no reservation behind");
    }

    [Fact]
    public async Task InitLauncher_AutoAccept_RidesTheEnqueuedRequest()
    {
        await NewLauncher().LaunchAsync(Project, autoCompletePullRequests: true, CancellationToken.None);

        var context = _enqueued.Should().ContainSingle().Subject.Context;
        context.Should().ContainKey(ContextKeys.AutoCompletePullRequests)
            .WhoseValue.Should().Be(true, "consent belongs to the launch that carried it");
    }

    [Fact]
    public async Task InitEndpoint_AutoAccept_TravelsFromTheBodyToTheRequest()
    {
        await ProjectInitEndpoints.InitAsync(
            Project, new InitLaunchRequest(AutoCompletePullRequests: true),
            NewLauncher(), CancellationToken.None);

        _enqueued.Single().Context!
            .Should().Contain(ContextKeys.AutoCompletePullRequests, true);
    }

    [Fact]
    public async Task InitEndpoint_NoBody_DoesNotAutoAccept()
    {
        await ProjectInitEndpoints.InitAsync(Project, request: null, NewLauncher(), CancellationToken.None);

        _enqueued.Single().Context!
            .Should().Contain(ContextKeys.AutoCompletePullRequests, false);
    }

    [Fact]
    public async Task InitEndpoint_Success_AnswersTheRunId()
    {
        var response = await ProjectInitEndpoints.InitAsync(
            Project, new InitLaunchRequest(AutoComplete), NewLauncher(), CancellationToken.None);

        StatusOf(response).Should().Be(StatusCodes.Status200OK);
        BodyOf(response).RunId.Should().Be(_enqueued.Single().RunId);
    }

    [Fact]
    public async Task InitEndpoint_AlreadyRunning_Answers409_WithTheRunId()
    {
        var first = await NewLauncher().LaunchAsync(Project, AutoComplete, CancellationToken.None);

        var response = await ProjectInitEndpoints.InitAsync(
            Project, new InitLaunchRequest(AutoComplete), NewLauncher(), CancellationToken.None);

        StatusOf(response).Should().Be(StatusCodes.Status409Conflict);
        BodyOf(response).RunId.Should().Be(first.RunId);
    }

    [Fact]
    public async Task InitEndpoint_NoCapacity_Answers503_WithTheReason()
    {
        _probe = DenyingProbe("namespace quota is full");

        var response = await ProjectInitEndpoints.InitAsync(
            Project, new InitLaunchRequest(AutoComplete), NewLauncher(), CancellationToken.None);

        StatusOf(response).Should().Be(StatusCodes.Status503ServiceUnavailable);
        BodyOf(response).Reason.Should().Be("namespace quota is full");
        BodyOf(response).RunId.Should().BeNull();
    }

    private static int? StatusOf(IResult result) => ((IStatusCodeHttpResult)result).StatusCode;

    private static InitLaunchResponse BodyOf(IResult result) =>
        (InitLaunchResponse)((IValueHttpResult)result).Value!;

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private InitRunLauncher NewLauncher() => new(
        ConfigLoader(), new ServerContext("agentsmith.yml"),
        new InitRunRepository(new AgentSmithDbContext(Options()), TimeProvider.System),
        NewAdmission(), NewQueue(), TimeProvider.System,
        NullLogger<InitRunLauncher>.Instance);

    private InitRunAdmission NewAdmission() => new(
        StubFootprintCalculator(), NewBudget(), new NoOpSandboxCorpseReaper(), _probe,
        NullLogger<InitRunAdmission>.Instance);

    private ICapacityBudget NewBudget() => new DbCapacityBudget(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        Microsoft.Extensions.Options.Options.Create(
            new CapacityBudgetOptions { MemoryLimit = _budgetMemory }));

    private IRedisJobQueue NewQueue()
    {
        var queue = new Mock<IRedisJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((r, _) => _enqueued.Add(r))
            .Returns(Task.CompletedTask);
        return queue.Object;
    }

    private static IConfigurationLoader ConfigLoader()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                [Project] = new()
                {
                    Name = Project,
                    Repos = [new RepoConnection { Name = "sample-server" }],
                },
            },
        };
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return loader.Object;
    }

    private static IRunFootprintCalculator StubFootprintCalculator()
    {
        var calculator = new Mock<IRunFootprintCalculator>();
        calculator.Setup(c => c.CalculateAsync(
                It.IsAny<ResolvedProject>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunFootprintBreakdown(
                [], "1", "4Gi", 1_000_000_000, 4L * 1024 * 1024 * 1024, [], "stub footprint"));
        return calculator.Object;
    }

    private static ISandboxCapacityProbe AdmittingProbe()
    {
        var probe = new Mock<ISandboxCapacityProbe>();
        probe.Setup(p => p.HasCapacityAsync(It.IsAny<RunFootprint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CapacityDecision.Admit());
        return probe.Object;
    }

    private static ISandboxCapacityProbe DenyingProbe(string reason)
    {
        var probe = new Mock<ISandboxCapacityProbe>();
        probe.Setup(p => p.HasCapacityAsync(It.IsAny<RunFootprint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CapacityDecision.Deny(reason));
        return probe.Object;
    }
}
