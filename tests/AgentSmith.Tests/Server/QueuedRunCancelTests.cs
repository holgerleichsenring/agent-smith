using AgentSmith.Tests.TestSupport;
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
            "p1", "42", "code", "github",
            "2026-07-10T12-00-00-a1b2", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var handled = await QueuedRunCancel.TryAsync(
            reserved, NewRepository(), _queue, _events, NewFinalizer(),
            NewTerminalWriter(), CancellationToken.None);

        handled.Should().BeTrue();
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.QueuedTickets.Should().BeEmpty();
        var finished = _published.OfType<RunFinishedEvent>().Single();
        finished.RunId.Should().Be(reserved);
        finished.Status.Should().Be("cancelled");

        // Project the terminal event — the queued row is finished as cancelled.
        await RunEventAppliers.Default().ApplyAsync(
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
            "p1", "42", "code", "github",
            "2026-07-10T12-00-00-a1b2", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var handled = await QueuedRunCancel.TryAsync(
            reserved, NewRepository(), _queue, _events, NewFinalizer(),
            NewTerminalWriter(), CancellationToken.None);

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
            "p1", "42", "code", "github",
            "2026-07-10T12-00-00-a1b2", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var handled = await QueuedRunCancel.TryAsync(
            reserved, NewRepository(), _queue, _events, NewFinalizer(),
            NewTerminalWriter(), CancellationToken.None);

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
                Id = "run-live", Project = "p1", Pipeline = "code",
                TicketId = "42", Status = "running", StartedAt = DateTimeOffset.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        var handled = await QueuedRunCancel.TryAsync(
            "run-live", NewRepository(), _queue, _events, NewFinalizer(),
            NewTerminalWriter(), CancellationToken.None);

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
            factory.Object, loader.Object,
            new AgentSmith.Application.Services.Claim.NoOpActiveRunLease(),
            new ServerContext("config.yaml"),
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

    /// <summary>
    /// 2026-08-24-ca23: the queued cancel is the SECOND entry point and never reaches
    /// CancelEnforcer. It publishes into a stream nobody drains — a queued run left the active
    /// set at its own waiting event — so before this phase the row stayed unfinished and the
    /// enforcer re-selected it every scan. The row must be terminal when the call returns.
    /// </summary>
    [Fact]
    public async Task Cancel_QueuedRun_ThroughTheEndpointsOwnPath_FinishesTheRowWithoutTheDrain()
    {
        var reserved = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "77", "code", "github",
            "2026-08-24T19-46-27-ca23", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var handled = await QueuedRunCancel.TryAsync(
            reserved, NewRepository(), _queue, _events, NewFinalizer(),
            NewTerminalWriter(), CancellationToken.None);

        handled.Should().BeTrue();
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == reserved);
        run.Status.Should().Be("cancelled", "nothing will deliver this run's terminal event");
        run.FinishedAt.Should().NotBeNull();
    }

    /// <summary>
    /// 2026-10-02-5f89d: an init's request sits in the JOB queue, which a queued cancel does
    /// not touch, and the consumer's gate used to ask only for a flagged UNFINISHED row — so a
    /// run cancelled while queued passed it and executed under a row that said cancelled.
    /// The consumer here has no ExecutePipelineUseCase: a leak through the gate publishes
    /// nothing, a refusal publishes its short-circuit.
    /// </summary>
    [Fact]
    public async Task QueuedRunCancel_InitRun_IsNeverStartedByTheConsumer()
    {
        const string runId = "2026-10-02T09-43-00-5f89";
        await new InitRunRepository(new AgentSmithDbContext(Options()), TimeProvider.System)
            .CreateQueuedRunAsync(runId, "p1", "init-project", ["repo-a"], "starting", CancellationToken.None);

        var handled = await QueuedRunCancel.TryAsync(
            runId, NewRepository(), _queue, _events, NewFinalizer(),
            NewTerminalWriter(), CancellationToken.None);
        _published.Clear();
        await NewConsumer(new PipelineRequest("p1", "init-project", IsInit: true, Headless: true, RunId: runId))
            .RunAsync(CancellationToken.None);

        handled.Should().BeTrue();
        _published.OfType<RunFinishedEvent>().Should().ContainSingle("the gate refused the run")
            .Which.RunId.Should().Be(runId);
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == runId);
        run.Status.Should().Be("cancelled");
        run.CancelRequested.Should().BeTrue("the row says who ended it");
        run.CancelReason.Should().Be("operator");
    }

    private AgentSmith.Application.Services.PipelineQueueConsumer NewConsumer(PipelineRequest request)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_events);
        services.AddSingleton<IActiveRunLease>(new AgentSmith.Application.Services.Claim.NoOpActiveRunLease());
        var scoped = new ServiceCollection();
        scoped.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        scoped.AddScoped<RunRepository>();
        var reader = new DbRunCancelStateReader(
            scoped.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
        return new AgentSmith.Application.Services.PipelineQueueConsumer(
            services.BuildServiceProvider(), new SingleRequestQueue(request), reader,
            "config.yaml", maxParallelJobs: 1, shutdownGraceSeconds: 5,
            NullLogger<AgentSmith.Application.Services.PipelineQueueConsumer>.Instance);
    }

    private sealed class SingleRequestQueue(PipelineRequest request) : IRedisJobQueue
    {
        public Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<PipelineRequest> ConsumeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return request;
            await Task.CompletedTask;
        }

        public Task<long> LenAsync(CancellationToken cancellationToken) => Task.FromResult(0L);
    }

    private CancelTerminalWriter NewTerminalWriter() => new(WriterServices());

    private ServiceProvider WriterServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton<QueuedRunProjection>();
        services.AddSingleton<RunFinalizationProjection>();
        return services.BuildServiceProvider();
    }
}
