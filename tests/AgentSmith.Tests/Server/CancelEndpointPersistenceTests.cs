using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0330: the cancel endpoint persists CancelRequested + CancelReason + the kill
/// deadline on the run row BEFORE returning — never only via the projector's
/// eventual event drain. A spawned run (no registry entry, live row) must NOT be
/// stale-cleared: the enforcer owns it now.
/// </summary>
public sealed class CancelEndpointPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<RunEvent> _published = [];
    private readonly IEventPublisher _events;
    private readonly RunCancellationRegistry _registry =
        new(NullLogger<RunCancellationRegistry>.Instance);

    public CancelEndpointPersistenceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.Database.Migrate();

        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((ev, _) => _published.Add(ev))
            .Returns(Task.CompletedTask);
        _events = events.Object;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Cancel_Endpoint_PersistsFlagSynchronously()
    {
        await SeedRunningRunAsync("run-1", jobId: "abc123def456");

        var before = DateTimeOffset.UtcNow;
        var result = await RunControlEndpoints.CancelAsync(
            "run-1", _registry, NewBroadcaster(), _events, NewRepository(),
            Mock.Of<ICapacityQueue>(), NewFinalizer(), TimeProvider.System,
            CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Accepted>();
        // The flag is on the ROW already — no projector drain happened here.
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == "run-1");
        run.CancelRequested.Should().BeTrue();
        run.CancelReason.Should().Be("operator");
        run.CancelDeadlineAt.Should().NotBeNull();
        run.CancelDeadlineAt!.Value.Should().BeCloseTo(
            before + CancelEnforcer.KillGrace, TimeSpan.FromSeconds(5));
        // The fanout event still goes out for live subscribers.
        _published.OfType<RunCancelRequestedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Cancel_SpawnedRun_NoRegistryEntry_IsNotStaleCleared()
    {
        await SeedRunningRunAsync("run-2", jobId: "abc123def456");

        await RunControlEndpoints.CancelAsync(
            "run-2", _registry, NewBroadcaster(), _events, NewRepository(),
            Mock.Of<ICapacityQueue>(), NewFinalizer(), TimeProvider.System,
            CancellationToken.None);

        // Pre-p0330 this published a synthetic RunFinished(cancelled) that marked
        // the row terminal while the pod ran on — the enforcer would then skip it.
        _published.OfType<RunFinishedEvent>().Should().BeEmpty(
            "a persisted live row is the enforcer's job, not a stale-clear");
    }

    // p0357 (p0330b): cancelling a RUNNING run terminalizes the ticket AT REQUEST
    // TIME. Pre-p0357 only the queued branch and the enforcer's force-kill did —
    // the cooperative cancel left the ticket in trigger_statuses and the next poll
    // re-claimed it within a cycle (observed live: status=New + stale in-progress tag).
    [Fact]
    public async Task CancelRunning_TerminalizesTicketSynchronously()
    {
        await SeedRunningRunAsync("run-3", jobId: "abc123def456");
        var ticketProvider = new Mock<ITicketProvider>();

        await RunControlEndpoints.CancelAsync(
            "run-3", _registry, NewBroadcaster(), _events, NewRepository(),
            Mock.Of<ICapacityQueue>(), NewObservableFinalizer(ticketProvider), TimeProvider.System,
            CancellationToken.None);

        ticketProvider.Verify(p => p.FinalizeAsync(
            new AgentSmith.Domain.Models.TicketId("42"), It.IsAny<string>(), "Rejected",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_UnknownRun_NoRowNoSnapshot_Returns404()
    {
        var result = await RunControlEndpoints.CancelAsync(
            "run-unknown", _registry, NewBroadcaster(), _events, NewRepository(),
            Mock.Of<ICapacityQueue>(), NewFinalizer(), TimeProvider.System,
            CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
        _published.Should().BeEmpty();
    }

    private async Task SeedRunningRunAsync(string runId, string? jobId)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "p1", Pipeline = "fix-bug", TicketId = "42",
            Status = "running", StartedAt = DateTimeOffset.UtcNow, JobId = jobId,
        });
        await ctx.SaveChangesAsync();
    }

    private RunRepository NewRepository() => new(new AgentSmithDbContext(Options()));

    private static JobsBroadcaster NewBroadcaster() => new(
        Mock.Of<IConnectionMultiplexer>(), Mock.Of<IRunEventFanout>(),
        NewRouter(), NullLogger<JobsBroadcaster>.Instance);

    private static RunEventRouter NewRouter() => new(
        Mock.Of<IRunEventFanout>(), new SandboxExpansionRegistry(),
        new SandboxDetailEventClassifier(), new SandboxActivityCoalescer(),
        new RunDbEventPersistence(null));

    // p0357: a finalizer whose ticket provider is observable — the running-cancel
    // branch must terminalize the ticket through it at request time.
    private static CancelledTicketFinalizer NewObservableFinalizer(Mock<ITicketProvider> ticketProvider)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["p1"] = new()
                {
                    Name = "p1",
                    Tracker = new TrackerConnection { Type = TrackerType.GitHub },
                    GithubTrigger = new WebhookTriggerConfig
                    {
                        TriggerStatuses = ["Approved"], DoneStatus = "closed", FailedStatus = "Rejected",
                    },
                },
            },
        };
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return new CancelledTicketFinalizer(
            factory.Object, loader.Object,
            new AgentSmith.Application.Services.Claim.NoOpActiveRunLease(),
            new ServerContext("config.yaml"), NullLogger<CancelledTicketFinalizer>.Instance);
    }

    private static CancelledTicketFinalizer NewFinalizer()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig());
        return new CancelledTicketFinalizer(
            Mock.Of<ITicketProviderFactory>(), loader.Object,
            new AgentSmith.Application.Services.Claim.NoOpActiveRunLease(),
            new ServerContext("config.yaml"), NullLogger<CancelledTicketFinalizer>.Instance);
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
