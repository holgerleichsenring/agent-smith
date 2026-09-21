using AgentSmith.Tests.TestSupport;
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
        _connection = MigratedStoreTemplate.OpenCopy();
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

    // 2026-09-21-c724c: this test used to HAND-APPLY the published event before asserting the
    // row — the production step that does not happen, because a run that never started has no
    // cursor on its stream. It passed on a broken tree. The assertion now comes from the tick.
    [Fact]
    public async Task Pump_TicketStatusLeftTriggerSet_EntryDropped_RowCancelledFromTheTickAlone()
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

    // ---- 2026-09-21-c724c: a head the claim service REFUSES ----------------------------
    // An operator saw thirteen runs "waiting for capacity" over an EMPTY queue table: every
    // entry had been dropped, and the drop only PUBLISHED its terminal event. A run that never
    // started was never in the active set, so no cursor exists on its stream and nothing drains
    // it. The row is written now, where it lives.

    private const string Refusal = "the tracker refuses the configured status";

    private static ClaimResult RefusedClaim() =>
        ClaimResult.Rejected(ClaimRejectionReason.TicketLastLeftUnmoved, Refusal);

    [Fact]
    public async Task QueueDrop_ARefusedHead_LeavesItsRunFinishedRatherThanQueued()
    {
        var harness = new Harness(_connection, ticketStatus: "Approved", claimResult: RefusedClaim());
        var reserved = await harness.EnqueueAsync("42");

        await harness.Pump.TickAsync(CancellationToken.None);

        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.QueuedTickets.Should().BeEmpty("a permanent rejection must not stay at the head");
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == reserved);
        run.Status.Should().Be("cancelled", "nothing will ever drain this run's terminal event");
        run.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task QueueDrop_ARefusedHead_CarriesTheRefusalAsItsReason()
    {
        var harness = new Harness(_connection, ticketStatus: "Approved", claimResult: RefusedClaim());
        var reserved = await harness.EnqueueAsync("42");

        await harness.Pump.TickAsync(CancellationToken.None);

        using var check = new AgentSmithDbContext(Options());
        check.Runs.Single(r => r.Id == reserved).Summary
            .Should().Be($"dropped from capacity queue: {Refusal}",
                "the row must say what refused it, not merely that it ended");
    }

    [Fact]
    public async Task QueueDrop_ARefusedHead_NudgesTheSurface()
    {
        var nudge = new RecordingNudge();
        var harness = new Harness(
            _connection, ticketStatus: "Approved", claimResult: RefusedClaim(), nudge: nudge);
        var reserved = await harness.EnqueueAsync("42");

        await harness.Pump.TickAsync(CancellationToken.None);

        nudge.RunIds.Should().ContainSingle(
                "the surface refetches on a nudge, on reconnect or on mount — it never polls")
            .Which.Should().Be(reserved);
    }

    [Fact]
    public async Task QueueDrop_ANudgeThatThrows_StillDropsTheEntry()
    {
        var harness = new Harness(
            _connection, ticketStatus: "Approved", claimResult: RefusedClaim(),
            nudge: new ThrowingNudge());
        var reserved = await harness.EnqueueAsync("42");

        await harness.Pump.TickAsync(CancellationToken.None);

        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.QueuedTickets.Should().BeEmpty("a hub that is down cannot fail a drop");
        using var check = new AgentSmithDbContext(Options());
        check.Runs.Single(r => r.Id == reserved).FinishedAt.Should().NotBeNull();
    }

    // The ORDER is load-bearing. Removing the entry first and then failing to finalize
    // reproduces exactly the state this phase exists to fix — entry gone, row queued, nothing
    // left to retry. Finalizing first leaves the head in place for the next tick.
    [Fact]
    public async Task QueueDrop_AFinalizeThatThrows_LeavesTheEntryForTheNextTick()
    {
        var harness = new Harness(
            _connection, ticketStatus: "Approved", claimResult: RefusedClaim(), finalizeThrows: true);
        var reserved = await harness.EnqueueAsync("42");

        var tick = async () => await harness.Pump.TickAsync(CancellationToken.None);
        await tick.Should().ThrowAsync<InvalidOperationException>();

        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.QueuedTickets.Should().ContainSingle("the head stays so the next tick retries it");
        harness.Published.Should().BeEmpty("nothing is announced that was not written");
        using var check = new AgentSmithDbContext(Options());
        check.Runs.Single(r => r.Id == reserved).FinishedAt
            .Should().BeNull("an orphaned row is the defect, not the fallback");
    }

    // The publish STAYS, and is safe because the projection's terminal transition is set-once.
    // The late event is deliberately given a different status and summary, so "changes nothing"
    // is something the row can actually show.
    [Fact]
    public async Task QueueDrop_ADrainedEventArrivingLater_ChangesNothing()
    {
        var harness = new Harness(_connection, ticketStatus: "Approved", claimResult: RefusedClaim());
        var reserved = await harness.EnqueueAsync("42");
        await harness.Pump.TickAsync(CancellationToken.None);
        DateTimeOffset? finishedByTheDrop;
        using (var ctx = new AgentSmithDbContext(Options()))
            finishedByTheDrop = ctx.Runs.Single(r => r.Id == reserved).FinishedAt;

        await ApplyAsync(new RunFinishedEvent(
            reserved, "failed", null, "a late arrival", DateTimeOffset.UtcNow.AddMinutes(5)));

        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == reserved);
        run.Status.Should().Be("cancelled");
        run.Summary.Should().Be($"dropped from capacity queue: {Refusal}");
        run.FinishedAt.Should().Be(finishedByTheDrop);
    }

    private sealed class RecordingNudge : IRunListNudge
    {
        public List<string> RunIds { get; } = [];

        public Task RunsChangedAsync(string runId, CancellationToken cancellationToken)
        {
            RunIds.Add(runId);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNudge : IRunListNudge
    {
        public Task RunsChangedAsync(string runId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no hub");
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(RunEvent ev)
    {
        await RunEventAppliers.Default().ApplyAsync(new AgentSmithDbContext(Options()), ev, CancellationToken.None);
    }

    private sealed class Harness
    {
        private readonly ICapacityQueue _queue;
        public CapacityQueuePump Pump { get; }
        public ClaimRequest? LastClaim { get; private set; }
        public List<RunEvent> Published { get; } = [];

        public Harness(
            SqliteConnection connection,
            string ticketStatus,
            ClaimResult? claimResult = null,
            IRunListNudge? nudge = null,
            bool finalizeThrows = false)
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
                .ReturnsAsync(claimResult ?? ClaimResult.Claimed());

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
                CapacityTestDoubles.AlwaysReserve(),
                CapacityTestDoubles.NoCorpses(),
                // 2026-09-21-c724c: the REAL drop — its terminal write goes through the real
                // finalization projection over this connection, so the row the tick leaves
                // behind is the row production would leave behind.
                new CapacityQueueDrop(
                    _queue,
                    finalizeThrows
                        ? new CancelTerminalWriter(ThrowingWriterServices())
                        : new CancelTerminalWriter(BuildServiceProvider(connection)),
                    events.Object,
                    nudge ?? new AgentSmith.Application.Services.Events.NoOpRunListNudge(),
                    NullLogger<CapacityQueueDrop>.Instance),
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
        // 2026-09-21-c724c: the finalization projection CancelTerminalWriter writes through —
        // without it the drop's terminal write is not exercised at all and the row's status
        // would only ever come from a hand-applied event.
        services.AddSingleton<QueuedRunProjection>();
        services.AddSingleton<RunFinalizationProjection>();
        return services.BuildServiceProvider();
    }

    // 2026-09-21-c724c: a terminal write that FAILS — the projection is real, the unit of work
    // it reads the run row from throws (a DB that is unreachable mid-tick).
    private static IServiceProvider ThrowingWriterServices()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Set<AgentSmith.Infrastructure.Persistence.Entities.Run>())
            .Throws(new InvalidOperationException("the store is unreachable"));
        var services = new ServiceCollection();
        services.AddScoped(_ => uow.Object);
        services.AddSingleton<QueuedRunProjection>();
        services.AddSingleton<RunFinalizationProjection>();
        return services.BuildServiceProvider();
    }
}
