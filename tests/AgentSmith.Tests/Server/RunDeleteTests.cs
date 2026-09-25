using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0337: deleting a run removes the run record and EVERY satellite it left
/// behind (children, lease, queue entry, checkpoint, expectation, dialogue
/// inbox). A non-terminal run is force-cleared first (pod terminated, lease
/// released, queue entry removed); a failed kill keeps the record. Bulk delete
/// is terminal-only.
/// <para>
/// 2026-09-20-9f00: and the force-clear DISARMS the ticket. A run with a result keeps
/// the hands-off; a run without one — queued, running or parked alike — would otherwise
/// be re-filed by the next poll. The deleter is composed here with a REAL
/// CancelledTicketFinalizer over a mocked provider, because the test that used to pin
/// the hands-off asserted a collaborator the deleter never received and so could not
/// fail.
/// </para>
/// </summary>
public sealed class RunDeleteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Mock<IJobSpawner> _spawner = new();
    private readonly Mock<IActiveRunLease> _lease = new();
    private readonly Mock<ITicketProvider> _ticketProvider = new();
    private readonly ICapacityQueue _queue;

    public RunDeleteTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.Database.Migrate();
        _queue = BuildDbQueue(_connection);
        // The DEFAULT answer, set once here so a test that wants a throwing provider can
        // override it — Moq takes the last matching Setup, and re-stating this default at
        // deleter-construction time (after the Arrange) would silently undo that override.
        _ticketProvider.Setup(p => p.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketFinalizeResult.Moved());
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Delete_TerminalRun_RemovesRowAndAllSatellites()
    {
        await SeedTerminalRunWithSatellitesAsync("run-done");

        var deleted = await new RunDeletionRepository(new AgentSmithDbContext(Options()))
            .DeleteAsync("run-done", CancellationToken.None);

        deleted.Should().Be(1);
        using var db = new AgentSmithDbContext(Options());
        db.Runs.Should().BeEmpty();
        db.RunSteps.Should().BeEmpty();
        db.RunSandboxes.Should().BeEmpty();
        db.RunCheckpoints.Should().BeEmpty();
        db.RunExpectations.Should().BeEmpty();
        db.DialogueAnswers.Should().BeEmpty();
        db.ActiveRuns.Should().BeEmpty();
        db.QueuedTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_RunningRun_TerminatesPodReleasesLease_ThenDeletes()
    {
        await SeedRunningRunAsync("run-live", jobId: "abc123def456");

        var outcome = await NewDeleter().DeleteAsync("run-live", CancellationToken.None);

        outcome.Should().Be(RunDeleteOutcome.Deleted);
        _spawner.Verify(s => s.TerminateAsync("abc123def456", It.IsAny<CancellationToken>()), Times.Once);
        _lease.Verify(l => l.ReleaseAsync(
            "p1", new TicketId("42"), "run-live", It.IsAny<CancellationToken>()), Times.Once);
        using var db = new AgentSmithDbContext(Options());
        db.Runs.Should().BeEmpty();
        db.ActiveRuns.Should().BeEmpty("the run's lease row is keyed by run id and cleared");
    }

    [Fact]
    public async Task Delete_RunningRun_TerminateFails_KeepsRecord()
    {
        _spawner.Setup(s => s.TerminateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("k8s API down"));
        await SeedRunningRunAsync("run-stuck", jobId: "bbbb00000000");

        var outcome = await NewDeleter().DeleteAsync("run-stuck", CancellationToken.None);

        outcome.Should().Be(RunDeleteOutcome.PodTerminationFailed);
        _lease.Verify(l => l.ReleaseAsync(
                It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never, "a failed kill must not release the lease and orphan a live pod");
        using var db = new AgentSmithDbContext(Options());
        db.Runs.Should().ContainSingle(r => r.Id == "run-stuck");
    }

    [Fact]
    public async Task Delete_QueuedRun_RemovesQueueEntryAndReservation_ThenDeletes()
    {
        var reserved = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "42", "code", "github",
            "2026-07-14T10-00-00-a1b2", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var outcome = await NewDeleter().DeleteAsync(reserved, CancellationToken.None);

        outcome.Should().Be(RunDeleteOutcome.Deleted);
        _spawner.Verify(s => s.TerminateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "a queued run has no spawned pod");
        using var db = new AgentSmithDbContext(Options());
        db.QueuedTickets.Should().BeEmpty();
        db.Runs.Should().BeEmpty("the reserved queued run row is removed");
    }

    // 2026-09-20-9f00: the queue entry alone is not durable — the ticket still sits in the
    // trigger statuses and re-pickup is gated on the lease the delete just released, so the
    // next poll would re-file it at the BACK of the queue.
    [Fact]
    public async Task RunDeleter_AQueuedRun_TerminalizesItsTicket()
    {
        var reserved = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "42", "code", "github",
            "2026-07-14T10-00-00-c3d4", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var outcome = await NewDeleter().DeleteAsync(reserved, CancellationToken.None);

        outcome.Should().Be(RunDeleteOutcome.Deleted);
        _ticketProvider.Verify(p => p.FinalizeAsync(
            new TicketId("42"), It.IsAny<string>(), "Blocked", It.IsAny<CancellationToken>()),
            Times.Once, "a deleted queued run whose ticket stays armed is re-filed by the next poll");
    }

    // The case an earlier cut left out: the native status only moves at run-end, so a RUNNING
    // run whose lease the deleter just released is in exactly the queued run's position.
    [Fact]
    public async Task RunDeleter_ARunningRun_TerminalizesItsTicket()
    {
        await SeedRunningRunAsync("run-live-2", jobId: "dddd00000000");

        var outcome = await NewDeleter().DeleteAsync("run-live-2", CancellationToken.None);

        outcome.Should().Be(RunDeleteOutcome.Deleted);
        _ticketProvider.Verify(p => p.FinalizeAsync(
            new TicketId("42"), It.IsAny<string>(), "Blocked", It.IsAny<CancellationToken>()),
            Times.Once, "a running run's ticket is still in the trigger statuses when the lease goes");
    }

    // The park this phase exists for: the dialogue ask gate parks WITHOUT touching the tracker,
    // so nothing on the row tells it from the master-question park that does. The predicate is
    // "no result yet", with no exception.
    [Fact]
    public async Task RunDeleter_AParkedRun_TerminalizesItsTicketLikeAnyRunWithNoResult()
    {
        await SeedParkedRunAsync("run-parked");

        var outcome = await NewDeleter().DeleteAsync("run-parked", CancellationToken.None);

        outcome.Should().Be(RunDeleteOutcome.Deleted);
        _ticketProvider.Verify(p => p.FinalizeAsync(
            new TicketId("42"), It.IsAny<string>(), "Blocked", It.IsAny<CancellationToken>()),
            Times.Once, "a park that never moved the ticket leaves it armed for the next poll");
    }

    // The deliberate hands-off that stays: a run WITH a result. Reachable now — the deleter
    // really holds the finalizer, so moving the terminalize out of the no-result branch
    // fails this.
    [Fact]
    public async Task RunDeleter_ARunWithAResult_LeavesItsTicketUntouched()
    {
        await SeedTerminalRunWithSatellitesAsync("run-finished");

        var outcome = await NewDeleter().DeleteAsync("run-finished", CancellationToken.None);

        outcome.Should().Be(RunDeleteOutcome.Deleted);
        _ticketProvider.Verify(p => p.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never, "a finished run's ticket is the operator's to move — deleting the record is not a verdict");
    }

    // Fail-soft: the delete is the operator's cleanup and a tracker that will not answer
    // must not turn it into a kept row.
    [Fact]
    public async Task RunDeleter_ATicketFinalizerThatFails_StillDeletesTheRun()
    {
        _ticketProvider.Setup(p => p.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tracker unreachable"));
        await SeedRunningRunAsync("run-tracker-down", jobId: "eeee00000000");

        var outcome = await NewDeleter().DeleteAsync("run-tracker-down", CancellationToken.None);

        outcome.Should().Be(RunDeleteOutcome.Deleted);
        using var db = new AgentSmithDbContext(Options());
        db.Runs.Should().BeEmpty("a tracker error must never keep the record the operator deleted");
    }

    [Fact]
    public async Task BulkDelete_ClearsTerminalOnly_LeavesRunningAndQueued()
    {
        await SeedTerminalRunWithSatellitesAsync("run-terminal");
        await SeedRunningRunAsync("run-running", jobId: "cccc00000000");
        var queued = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "99", "code", "github",
            "2026-07-14T10-00-00-e5f6", "waiting", ["repo-a"],
            InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var deleted = await NewDeleter().DeleteTerminalAsync(CancellationToken.None);

        deleted.Should().Be(1);
        _spawner.Verify(s => s.TerminateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "bulk clear is terminal-only and never force-kills");
        using var db = new AgentSmithDbContext(Options());
        db.Runs.Select(r => r.Id).Should().BeEquivalentTo(new[] { "run-running", queued });
    }

    [Fact]
    public async Task ADeletedRun_DoesNotReleaseTheLeaseANewerRunHolds()
    {
        // p0459, the live defect: an operator deleted an older, still non-terminal
        // run for a ticket a NEWER run had since claimed. Force-clear released the
        // lease BY TICKET, the poller found the ticket free, and two runs worked
        // the same branch. Proven against the real DB lease, not a mock.
        await SeedRunningRunAsync("run-old", jobId: "aaaa00000000");
        var lease = new DbActiveRunLease(LeaseScopeFactory());
        await lease.AttachRunAsync(
            "p1", new TicketId("42"), "run-new", "bbbb00000000", CancellationToken.None);

        var outcome = await NewDeleter(lease).DeleteAsync("run-old", CancellationToken.None);

        outcome.Should().Be(RunDeleteOutcome.Deleted);
        var held = await lease.GetByTicketAsync("p1", new TicketId("42"), CancellationToken.None);
        held!.RunId.Should().Be("run-new", "deleting an older run must not strip the newer run's claim");
        // 2026-09-20-9f00: and the disarm is CONDITIONAL for the same reason — the finalizer's
        // ownership guard reads that lease and skips, so the newer run keeps its ticket.
        _ticketProvider.Verify(p => p.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never, "the ticket belongs to the run that reclaimed it");
    }

    private IServiceScopeFactory LeaseScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddScoped<ActiveRunRepository>();
        services.AddLogging();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private RunDeleter NewDeleter(IActiveRunLease? lease = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_spawner.Object);
        var provider = services.BuildServiceProvider();
        var uow = new AgentSmithDbContext(Options());
        return new RunDeleter(
            provider, new RunRepository(uow), new RunDeletionRepository(uow),
            lease ?? _lease.Object, _queue, NewTicketFinalizer(lease ?? _lease.Object),
            NullLogger<RunDeleter>.Instance);
    }

    // 2026-09-20-9f00: the REAL finalizer over a mocked provider — the collaborator the
    // hands-off assertion used to name without ever receiving it. The project it resolves
    // carries a failed_status, so the status the finalize asks for is observable too.
    private CancelledTicketFinalizer NewTicketFinalizer(IActiveRunLease lease)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_ticketProvider.Object);
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["p1"] = new()
                {
                    Name = "p1",
                    Tracker = new TrackerConnection { Name = "tracker-a", Type = TrackerType.GitHub },
                    GithubTrigger = new WebhookTriggerConfig { FailedStatus = "Blocked" },
                },
            },
        });
        return new CancelledTicketFinalizer(
            factory.Object, loader.Object, lease, new ServerContext("agentsmith.yml"),
            NullLogger<CancelledTicketFinalizer>.Instance);
    }

    // 2026-09-20-9f00: a PARKED run — no result, no job, and (for the dialogue ask gate's
    // park) a ticket nothing ever moved.
    private async Task SeedParkedRunAsync(string runId)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "p1", Pipeline = "code", TicketId = "42",
            Platform = "github", Status = "waiting_for_input",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedRunningRunAsync(string runId, string jobId)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "p1", Pipeline = "code", TicketId = "42",
            Platform = "github", Status = "running", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            JobId = jobId,
        });
        ctx.ActiveRuns.Add(new ActiveRun
        {
            Project = "p1", TicketId = "42", RunId = runId, JobId = jobId,
            ClaimedAt = DateTimeOffset.UtcNow, HeartbeatAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedTerminalRunWithSatellitesAsync(string runId)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "p1", Pipeline = "code", TicketId = "7",
            Platform = "github", Status = "failed",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5), FinishedAt = DateTimeOffset.UtcNow,
        });
        ctx.RunSteps.Add(new RunStep { RunId = runId });
        ctx.RunSandboxes.Add(new RunSandbox { RunId = runId });
        ctx.RunCheckpoints.Add(new RunCheckpoint { RunId = runId, DialogueJobId = "d1", Project = "p1", TicketId = "7" });
        ctx.RunExpectations.Add(new RunExpectation { RunId = runId });
        ctx.DialogueAnswers.Add(new DialogueAnswerEntry { DialogueJobId = "d1", QuestionId = "q1" });
        // A lease + queue entry keyed to THIS run must go too.
        ctx.ActiveRuns.Add(new ActiveRun
        {
            Project = "p1", TicketId = "7", RunId = runId,
            ClaimedAt = DateTimeOffset.UtcNow, HeartbeatAt = DateTimeOffset.UtcNow,
        });
        ctx.QueuedTickets.Add(new QueuedTicket
        {
            Project = "p1", TicketId = "7", Pipeline = "code", Platform = "github",
            ReservedRunId = runId, Reason = "x", EnqueuedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

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
