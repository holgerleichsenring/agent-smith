using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Tests.Spawning;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-09-18-c1a7: a ticket whose last run could not move it is claimed ONCE, through any door.
/// The stop lives in the claim service — the single point the poller funnel and the capacity pump
/// both pass through — and the pump's RESUME of a parked run never enters it.
/// </summary>
public sealed class UnmovedTicketClaimTests : IDisposable
{
    private const string Project = "p1";
    private const string Ticket = "42";
    private const string Tracker = "tracker-a";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly IServiceProvider _services;
    private readonly IUnmovedTicketStore _store;
    private readonly Mock<IRedisJobQueue> _jobQueue = new();
    private readonly List<AgentSmith.Contracts.Events.RunEvent> _published = [];

    public UnmovedTicketClaimTests()
    {
        _services = BuildServiceProvider(_connection);
        _store = new DbUnmovedTicketStore(_services.GetRequiredService<IServiceScopeFactory>());
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Claim_TicketLastLeftUnmoved_IsRejectedWithTheNewReason()
    {
        await RecordUnmovedAsync();

        var result = await ClaimService().ClaimAsync(Request(), Config(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.Rejected);
        result.Rejection.Should().Be(ClaimRejectionReason.TicketLastLeftUnmoved);
        _jobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Claim_RejectionDetail_CarriesTheConfiguredValue()
    {
        await RecordUnmovedAsync();

        var result = await ClaimService().ClaimAsync(Request(), Config(), CancellationToken.None);

        // It names the two places the value can be corrected and nothing else: the dashboard has
        // no retry affordance to send an operator to.
        result.Error.Should().Contain("Failed").And.Contain(Tracker).And.Contain(Project)
            .And.NotContain("run view");
    }

    [Fact]
    public async Task Claim_TicketWhoseLatestRunMovedIt_IsAllowed()
    {
        var result = await ClaimService().ClaimAsync(Request(), Config(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.Claimed, "a run that moved its ticket records nothing");
    }

    [Fact]
    public async Task Claim_SameTicketThroughTheWebhookDispatcher_IsRejectedToo()
    {
        await RecordUnmovedAsync();

        var spawn = await Funnel().ExecuteAsync(
            Config(), Config().Projects[Project], "code",
            new IncomingTicketEnvelope { TicketId = Ticket, Platform = "github" },
            new WebhookTriggerConfig { DefaultPipeline = "code" },
            CancellationToken.None);

        spawn.ClaimResults.Should().ContainSingle()
            .Which.Rejection.Should().Be(ClaimRejectionReason.TicketLastLeftUnmoved);
    }

    [Fact]
    public async Task Claim_SameTicketThroughTheCapacityPump_IsRejectedToo()
    {
        await RecordUnmovedAsync();
        var pump = Pump(out _);
        await EnqueueAsync(isResume: false);

        await pump.TickAsync(CancellationToken.None);

        _jobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The pump peeks the HEAD and nothing behind it, so an entry that can never be claimed would
    // stop every other ticket in the project. A refusal is permanent by definition: it leaves.
    [Fact]
    public async Task Claim_RejectedHeadOfTheCapacityQueue_IsDroppedInsteadOfWedgingTheQueue()
    {
        await RecordUnmovedAsync();
        var pump = Pump(out _);
        var reserved = await EnqueueAsync(isResume: false);

        await pump.TickAsync(CancellationToken.None);

        (await Queue().CountAsync(CancellationToken.None)).Should().Be(0,
            "a head that can never be claimed must not hold the queue behind it");
        _published.OfType<RunFinishedEvent>().Should().ContainSingle()
            .Which.Should().Match<RunFinishedEvent>(e =>
                e.RunId == reserved && e.Status == "cancelled" && e.Summary!.Contains("Failed"));
    }

    [Fact]
    public async Task Claim_AfterTheRejectedHeadWasDropped_TheNextTicketLaunches()
    {
        await RecordUnmovedAsync();
        var pump = Pump(out _);
        await EnqueueAsync(isResume: false);
        await Queue().EnqueueAsync(Candidate("43", isResume: false), CancellationToken.None);

        await pump.TickAsync(CancellationToken.None);
        await pump.TickAsync(CancellationToken.None);

        _jobQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.TicketId!.Value == "43"), It.IsAny<CancellationToken>()),
            Times.Once, "the ticket behind the refused one is not held by it");
    }

    [Fact]
    public async Task Claim_ResumeOfAParkedRun_IsNotRejected()
    {
        await RecordUnmovedAsync();
        var pump = Pump(out var resumeQueue);
        var runId = await EnqueueAsync(isResume: true);
        await MarkWaitingForInputAsync(runId);

        await pump.TickAsync(CancellationToken.None);

        resumeQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.RunId == runId), It.IsAny<CancellationToken>()), Times.Once,
            "the resume never enters the claim service, so the refusal cannot reach it");
    }

    [Fact]
    public async Task Claim_AfterTheTrackerDocumentChanged_IsAllowedAgain()
    {
        await SaveConfigDocumentAsync("tracker", Tracker, version: 1);
        await RecordUnmovedAsync();

        await SaveConfigDocumentAsync("tracker", Tracker, version: 2);

        var result = await ClaimService().ClaimAsync(Request(), Config(), CancellationToken.None);
        result.Outcome.Should().Be(ClaimOutcome.Claimed);
    }

    [Fact]
    public async Task Claim_AfterTheProjectDocumentChanged_IsAllowedAgain()
    {
        await SaveConfigDocumentAsync("project", Project, version: 7);
        await RecordUnmovedAsync();

        await SaveConfigDocumentAsync("project", Project, version: 8);

        var result = await ClaimService().ClaimAsync(Request(), Config(), CancellationToken.None);
        result.Outcome.Should().Be(ClaimOutcome.Claimed);
    }

    [Fact]
    public async Task Claim_AfterTheOperatorRetriedFromTheRunView_IsAllowedAgain()
    {
        await RecordUnmovedAsync();
        var tickets = new Mock<ITicketProvider>();
        // 2026-09-21-1fa0: the tracker says it moved the ticket, and only that answer releases it.
        tickets.Setup(t => t.TransitionToAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(tickets.Object);
        var retry = new NotImplementableRetryService(
            new InMemorySpecSetPointerStore(), _store, factory.Object,
            NullLogger<NotImplementableRetryService>.Instance);

        var moved = await retry.RetryAsync(Config().Projects[Project], Ticket, CancellationToken.None);

        moved.Should().Be(RetryOutcome.Retried);
        var result = await ClaimService().ClaimAsync(Request(), Config(), CancellationToken.None);
        result.Outcome.Should().Be(ClaimOutcome.Claimed);
    }

    private Task RecordUnmovedAsync() => _store.RecordAsync(
        new UnmovedTicketFact(Project, Ticket, Tracker, "Failed",
            TicketFinalizeOutcome.TrackerRejectedTheStatus),
        CancellationToken.None);

    private TicketClaimService ClaimService()
    {
        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");
        claimLock.Setup(l => l.ReleaseAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var transitioner = new Mock<ITicketStatusTransitioner>();
        transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Succeeded());
        var transitionerFactory = new Mock<ITicketStatusTransitionerFactory>();
        transitionerFactory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(transitioner.Object);
        return new TicketClaimService(
            claimLock.Object, _store, transitionerFactory.Object, _jobQueue.Object,
            new NoOpActiveRunLease(), new InMemoryTakenTicketStore(),
            NullLogger<TicketClaimService>.Instance);
    }

    private SpawnPipelineRunsUseCase Funnel() => new(
        ClaimService(),
        CapacityTestDoubles.StubCalculator(),
        CapacityTestDoubles.AlwaysReserve(),
        Queue(),
        CapacityTestDoubles.NoCorpses(), CapacityTestDoubles.NoHolds(),
        CapacityTestDoubles.AlwaysAdmit(),
        ApprovedSetDoubles.Carrier(),
        CapacityTestDoubles.NoNudge(),
        _store,
        NullLogger<SpawnPipelineRunsUseCase>.Instance);

    private CapacityQueuePump Pump(out Mock<IRedisJobQueue> resumeQueue)
    {
        resumeQueue = new Mock<IRedisJobQueue>();
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketId id, CancellationToken _) =>
                new Ticket(id, "title", "desc", null, "Approved", "github"));
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Config());
        return new CapacityQueuePump(
            Queue(), ClaimService(), factory.Object,
            CapacityTestDoubles.AlwaysReserve(), CapacityTestDoubles.NoCorpses(),
            // 2026-09-21-c724c: the drop WRITES its run's terminal state through the real
            // finalization projection and publishes as before.
            new CapacityQueueDrop(
                Queue(), new CancelTerminalWriter(_services), Publisher(),
                CapacityTestDoubles.NoNudge(), NullLogger<CapacityQueueDrop>.Instance),
            new DbRunCancelStateReader(_services.GetRequiredService<IServiceScopeFactory>()),
            new ResumeRunLauncher(
                _services, new NoOpActiveRunLease(), resumeQueue.Object, Queue(),
                NullLogger<ResumeRunLauncher>.Instance),
            loader.Object, "config.yaml", NullLogger<CapacityQueuePump>.Instance);
    }

    private IEventPublisher Publisher()
    {
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(
                It.IsAny<AgentSmith.Contracts.Events.RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AgentSmith.Contracts.Events.RunEvent, CancellationToken>(
                (ev, _) => _published.Add(ev))
            .Returns(Task.CompletedTask);
        return events.Object;
    }

    private Task<string> EnqueueAsync(bool isResume) =>
        Queue().EnqueueAsync(Candidate(Ticket, isResume), CancellationToken.None);

    private static CapacityQueueCandidate Candidate(string ticketId, bool isResume) => new(
        Project, ticketId, "code", "github",
        AgentSmith.Application.Services.RunIdGenerator.Generate(DateTimeOffset.UtcNow),
        "waiting for sandbox capacity", ["repo-a"],
        InitialContextJson: "{}", PlanAnswersJson: null, IsResume: isResume);

    private async Task MarkWaitingForInputAsync(string runId)
    {
        using var context = Context();
        var run = await context.Runs.FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null)
        {
            run = new Run { Id = runId, Project = Project, TicketId = Ticket, Pipeline = "code" };
            context.Runs.Add(run);
        }
        run.Status = "waiting_for_input";
        await context.SaveChangesAsync();
    }

    private async Task SaveConfigDocumentAsync(string type, string id, int version)
    {
        using var context = Context();
        var row = await context.ConfigEntities
            .FirstOrDefaultAsync(c => c.EntityType == type && c.EntityId == id);
        if (row is null)
        {
            row = new ConfigEntity { EntityType = type, EntityId = id, Doc = "{}", UpdatedBy = "operator" };
            context.ConfigEntities.Add(row);
        }
        row.Version = version;
        await context.SaveChangesAsync();
    }

    private ICapacityQueue Queue() =>
        new DbCapacityQueue(_services.GetRequiredService<IServiceScopeFactory>());

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            [Project] = new()
            {
                Name = Project,
                Repos = [new RepoConnection { Name = "repo-a" }],
                Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
                GithubTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "code", TriggerStatuses = ["Approved"], DoneStatus = "closed",
                },
            },
        },
    };

    private static ClaimRequest Request() =>
        new("GitHub", Project, new TicketId(Ticket), "code");

    private static IServiceProvider BuildServiceProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<QueuedTicketRepository>();
        services.AddScoped<RunRepository>();
        services.AddScoped<UnmovedTicketRepository>();
        // 2026-09-21-c724c: what CancelTerminalWriter writes through.
        services.AddSingleton<QueuedRunProjection>();
        services.AddSingleton<RunFinalizationProjection>();
        return services.BuildServiceProvider();
    }
}
