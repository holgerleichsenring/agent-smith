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
        new SandboxExpansionRegistry(), NullLogger<JobsBroadcaster>.Instance);

    private static CancelledTicketFinalizer NewFinalizer()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig());
        return new CancelledTicketFinalizer(
            Mock.Of<ITicketProviderFactory>(), loader.Object,
            new ServerContext("config.yaml"), NullLogger<CancelledTicketFinalizer>.Instance);
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
