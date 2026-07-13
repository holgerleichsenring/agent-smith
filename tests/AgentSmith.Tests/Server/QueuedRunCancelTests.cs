using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0320c: cancelling a QUEUED run is pure bookkeeping — nothing is executing,
/// so no registry roundtrip: the queue entry is deleted and the row finishes
/// "cancelled" via the terminal event. p0330: the TICKET is terminalized too —
/// the queue entry alone is not durable, the ticket still sits in
/// trigger_statuses and the next poll would re-claim it as a fresh run.
/// </summary>
public sealed class QueuedRunCancelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ICapacityQueue _queue;
    private readonly List<RunEvent> _published = [];
    private readonly IEventPublisher _events;
    private readonly Mock<ITicketProvider> _ticketProvider = new();

    public QueuedRunCancelTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.Database.Migrate();
        _queue = BuildDbQueue(_connection);

        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((ev, _) => _published.Add(ev))
            .Returns(Task.CompletedTask);
        _events = events.Object;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Cancel_QueuedRun_RemovesEntry_MarksCancelled()
    {
        var reserved = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "42", "fix-bug", "github",
            "2026-07-10T12-00-00-a1b2", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var handled = await RunControlEndpoints.TryCancelQueuedAsync(
            reserved, NewRepository(), _queue, _events, NewFinalizer(), CancellationToken.None);

        handled.Should().BeTrue();
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.QueuedTickets.Should().BeEmpty();
        var finished = _published.OfType<RunFinishedEvent>().Single();
        finished.RunId.Should().Be(reserved);
        finished.Status.Should().Be("cancelled");

        // Project the terminal event — the queued row is finished as cancelled.
        await new RunEventApplier().ApplyAsync(
            new AgentSmithDbContext(Options()), finished, CancellationToken.None);
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == reserved);
        run.Status.Should().Be("cancelled");
        run.FinishedAt.Should().NotBeNull();
    }

    // p0330: the queued cancel must terminalize the tracker ticket via the
    // failed_status fallback chain, so the next poll cannot re-claim it.
    [Fact]
    public async Task CancelQueued_TerminalizesTicket_NoRepoll()
    {
        var reserved = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "42", "fix-bug", "github",
            "2026-07-10T12-00-00-a1b2", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var handled = await RunControlEndpoints.TryCancelQueuedAsync(
            reserved, NewRepository(), _queue, _events, NewFinalizer(), CancellationToken.None);

        handled.Should().BeTrue();
        _ticketProvider.Verify(p => p.FinalizeAsync(
            new TicketId("42"),
            It.Is<string>(c => c.Contains("Cancelled")),
            "Rejected",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // p0330: a tracker write failure is fail-soft — the cancel still succeeds.
    [Fact]
    public async Task CancelQueued_TrackerThrows_CancelStillSucceeds()
    {
        _ticketProvider.Setup(p => p.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tracker down"));
        var reserved = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "42", "fix-bug", "github",
            "2026-07-10T12-00-00-a1b2", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var handled = await RunControlEndpoints.TryCancelQueuedAsync(
            reserved, NewRepository(), _queue, _events, NewFinalizer(), CancellationToken.None);

        handled.Should().BeTrue("a tracker error must never block the cancel");
        _published.OfType<RunFinishedEvent>().Single().Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task Cancel_RunningRun_NotHandledHere_FallsThroughToRegistryPath()
    {
        using (var ctx = new AgentSmithDbContext(Options()))
        {
            ctx.Runs.Add(new AgentSmith.Infrastructure.Persistence.Entities.Run
            {
                Id = "run-live", Project = "p1", Pipeline = "fix-bug",
                TicketId = "42", Status = "running", StartedAt = DateTimeOffset.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        var handled = await RunControlEndpoints.TryCancelQueuedAsync(
            "run-live", NewRepository(), _queue, _events, NewFinalizer(), CancellationToken.None);

        handled.Should().BeFalse("a live run cancels through the registry, not the queue path");
        _published.Should().BeEmpty();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private RunRepository NewRepository() => new(new AgentSmithDbContext(Options()));

    private CancelledTicketFinalizer NewFinalizer()
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_ticketProvider.Object);
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
            factory.Object, loader.Object, new ServerContext("config.yaml"),
            NullLogger<CancelledTicketFinalizer>.Instance);
    }

    private static ICapacityQueue BuildDbQueue(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<QueuedTicketRepository>();
        return new DbCapacityQueue(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }
}
