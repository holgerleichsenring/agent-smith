using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Services;
using AgentSmith.Tests.Spawning;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0320c: the CapacityQueuePump against a REAL SQLite-backed queue. A fitting
/// head is claimed with its reserved run id (the queued row becomes the running
/// row via the applier upsert) and leaves the queue; a head whose ticket the
/// operator moved out of the trigger statuses is dropped and its row cancelled.
/// </summary>
public sealed class CapacityQueuePumpTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CapacityQueuePumpTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Pump_HeadFits_ClaimsWithReservedRunId_RowBecomesRunning()
    {
        var harness = new Harness(_connection, ticketStatus: "Approved");
        var reserved = await harness.EnqueueAsync("42");

        await harness.Pump.TickAsync(CancellationToken.None);

        harness.LastClaim.Should().NotBeNull();
        harness.LastClaim!.ExistingRunId.Should().Be(reserved);
        harness.LastClaim.PipelineName.Should().Be("fix-bug");
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.QueuedTickets.Should().BeEmpty("the launched head leaves the queue");

        // The launched run starts on the SAME id — the queued row becomes running.
        await ApplyAsync(new RunStartedEvent(
            reserved, "ticket", "fix-bug", ["repo-a"], DateTimeOffset.UtcNow,
            "claude", "42", Project: "p1", Platform: "github"));
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single();
        run.Id.Should().Be(reserved);
        run.Status.Should().Be("running");
        run.FinishedAt.Should().BeNull();
    }

    [Fact]
    public async Task Pump_TicketStatusLeftTriggerSet_EntryDropped_RowCancelled()
    {
        var harness = new Harness(_connection, ticketStatus: "Closed");
        var reserved = await harness.EnqueueAsync("42");

        await harness.Pump.TickAsync(CancellationToken.None);

        harness.LastClaim.Should().BeNull("a stale entry is dropped, never claimed");
        harness.Published.Should().ContainSingle()
            .Which.Should().BeOfType<RunFinishedEvent>()
            .Which.Should().Match<RunFinishedEvent>(e =>
                e.RunId == reserved && e.Status == "cancelled");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.QueuedTickets.Should().BeEmpty();

        // Project the published terminal event: the queued row finishes cancelled.
        await ApplyAsync(harness.Published.OfType<RunFinishedEvent>().Single());
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == reserved);
        run.Status.Should().Be("cancelled");
        run.FinishedAt.Should().NotBeNull();
    }

    // p0330: a cancel persisted while the entry waited at the head drops the
    // entry WITHOUT claiming — the run finishes 'cancelled', never launches.
    [Fact]
    public async Task Pump_CancelledHead_DroppedWithoutClaim()
    {
        var harness = new Harness(_connection, ticketStatus: "Approved");
        var reserved = await harness.EnqueueAsync("42");
        using (var ctx = new AgentSmithDbContext(Options()))
        {
            await new AgentSmith.Infrastructure.Persistence.Repositories.RunRepository(ctx)
                .MarkCancelRequestedAsync(
                    reserved, "operator", DateTimeOffset.UtcNow.AddSeconds(30), CancellationToken.None);
        }

        await harness.Pump.TickAsync(CancellationToken.None);

        harness.LastClaim.Should().BeNull("a cancelled head must never be claimed");
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.QueuedTickets.Should().BeEmpty("the cancelled entry leaves the queue");
        harness.Published.OfType<RunFinishedEvent>().Single()
            .Should().Match<RunFinishedEvent>(e => e.RunId == reserved && e.Status == "cancelled");
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(RunEvent ev)
    {
        await new RunEventApplier().ApplyAsync(new AgentSmithDbContext(Options()), ev, CancellationToken.None);
    }

    private sealed class Harness
    {
        private readonly ICapacityQueue _queue;
        public CapacityQueuePump Pump { get; }
        public ClaimRequest? LastClaim { get; private set; }
        public List<RunEvent> Published { get; } = [];

        public Harness(SqliteConnection connection, string ticketStatus)
        {
            _queue = BuildDbQueue(connection);
            var config = new AgentSmithConfig
            {
                Projects = new Dictionary<string, ResolvedProject>
                {
                    ["p1"] = new()
                    {
                        Name = "p1",
                        Repos = [new RepoConnection { Name = "repo-a" }],
                        Tracker = new TrackerConnection { Type = TrackerType.GitHub },
                        GithubTrigger = new WebhookTriggerConfig
                        {
                            TriggerStatuses = ["Approved"], DoneStatus = "closed",
                        },
                    },
                },
            };

            var claimService = new Mock<ITicketClaimService>();
            claimService.Setup(c => c.ClaimAsync(
                    It.IsAny<ClaimRequest>(), It.IsAny<AgentSmithConfig>(), It.IsAny<CancellationToken>()))
                .Callback<ClaimRequest, AgentSmithConfig, CancellationToken>((r, _, _) => LastClaim = r)
                .ReturnsAsync(ClaimResult.Claimed());

            var provider = new Mock<ITicketProvider>();
            provider.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TicketId id, CancellationToken _) =>
                    new Ticket(id, "title", "desc", null, ticketStatus, "github"));
            var factory = new Mock<ITicketProviderFactory>();
            factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);

            var events = new Mock<IEventPublisher>();
            events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
                .Callback<RunEvent, CancellationToken>((ev, _) => Published.Add(ev))
                .Returns(Task.CompletedTask);

            var loader = new Mock<IConfigurationLoader>();
            loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);

            Pump = new CapacityQueuePump(
                _queue, claimService.Object, factory.Object,
                CapacityTestDoubles.PassthroughResolver(),
                CapacityTestDoubles.NoOrchestrator(),
                CapacityTestDoubles.AlwaysAdmit(),
                events.Object,
                // p0330: the pre-claim cancel gate reads the REAL persisted flag.
                new DbRunCancelStateReader(BuildScopeFactory(connection)),
                // p0327: resume entries launch via lease + direct job enqueue.
                new AgentSmith.Server.Services.ResumeRunLauncher(
                    BuildServiceProvider(connection),
                    new AgentSmith.Application.Services.Claim.NoOpActiveRunLease(),
                    Moq.Mock.Of<AgentSmith.Contracts.Services.IRedisJobQueue>(),
                    _queue,
                    NullLogger<AgentSmith.Server.Services.ResumeRunLauncher>.Instance),
                loader.Object, "config.yaml",
                NullLogger<CapacityQueuePump>.Instance);
        }

        public Task<string> EnqueueAsync(string ticketId) =>
            _queue.EnqueueAsync(new CapacityQueueCandidate(
                "p1", ticketId, "fix-bug", "github",
                AgentSmith.Application.Services.RunIdGenerator.Generate(DateTimeOffset.UtcNow),
                "waiting for sandbox capacity", ["repo-a"],
                InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);
    }

    private static ICapacityQueue BuildDbQueue(SqliteConnection connection) =>
        new DbCapacityQueue(BuildScopeFactory(connection));

    private static IServiceScopeFactory BuildScopeFactory(SqliteConnection connection) =>
        BuildServiceProvider(connection).GetRequiredService<IServiceScopeFactory>();

    private static IServiceProvider BuildServiceProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<QueuedTicketRepository>();
        services.AddScoped<RunRepository>(); // p0330: cancel-gate reads
        return services.BuildServiceProvider();
    }
}
