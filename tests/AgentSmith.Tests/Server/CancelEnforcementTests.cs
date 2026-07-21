using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
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
/// p0330: cancel is persistent state, enforced. The enforcer scans the DB for
/// CancelRequested + elapsed kill deadline and force-kills via IJobSpawner —
/// state lives ONLY in the run row, so it survives a server restart by
/// construction. Terminal transitions are set-once: a late RunFinished from a
/// killed pod cannot overwrite 'cancelled'.
/// </summary>
public sealed class CancelEnforcementTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Mock<IJobSpawner> _spawner = new();
    private readonly Mock<IActiveRunLease> _lease = new();
    private readonly Mock<ITicketProvider> _ticketProvider = new();
    private readonly List<RunEvent> _published = [];
    private readonly IEventPublisher _events;

    public CancelEnforcementTests()
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

    // p0355: a superseded run must not finalize a reclaimed ticket.
    [Fact]
    public async Task Finalizer_TicketReclaimedByNewerRun_SupersededRunDoesNotTransition()
    {
        _lease.Setup(l => l.GetByTicketAsync("p1", new TicketId("42"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSmith.Contracts.Models.StaleLease("p1", new TicketId("42"), "run-new", null));

        await NewFinalizer().FinalizeAsync("p1", "42", "run-old", "<b>cancelled</b>", CancellationToken.None);

        _ticketProvider.Verify(p => p.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // p0355: when this run still owns the ticket (or no run does), the finalize runs.
    [Fact]
    public async Task Finalizer_SameRunOwnsTicket_Transitions()
    {
        _lease.Setup(l => l.GetByTicketAsync("p1", new TicketId("42"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSmith.Contracts.Models.StaleLease("p1", new TicketId("42"), "run-old", null));

        await NewFinalizer().FinalizeAsync("p1", "42", "run-old", "<b>cancelled</b>", CancellationToken.None);

        _ticketProvider.Verify(p => p.FinalizeAsync(
            new TicketId("42"), It.IsAny<string>(), "Rejected", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_SpawnedRun_TerminatesJobAndFinalizesCancelled()
    {
        await SeedRunAsync("run-spawned", jobId: "abc123def456",
            cancelRequested: true, deadline: DateTimeOffset.UtcNow.AddSeconds(-1));

        var enforced = await NewEnforcer().RunOnceAsync(CancellationToken.None);

        enforced.Should().Be(1);
        _spawner.Verify(s => s.TerminateAsync("abc123def456", It.IsAny<CancellationToken>()), Times.Once);
        var finished = _published.OfType<RunFinishedEvent>().Single();
        finished.RunId.Should().Be("run-spawned");
        finished.Status.Should().Be("cancelled");
        _lease.Verify(l => l.ReleaseAsync("p1", new TicketId("42"), It.IsAny<CancellationToken>()), Times.Once);
        _ticketProvider.Verify(p => p.FinalizeAsync(
            new TicketId("42"), It.IsAny<string>(), "Rejected", It.IsAny<CancellationToken>()), Times.Once);

        // The event path finalizes the row (single-writer projector).
        await ApplyAsync(finished);
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == "run-spawned");
        run.Status.Should().Be("cancelled");
        run.FinishedAt.Should().NotBeNull();
    }

    // The deadline is DURABLE: nothing was registered in this "process" (no
    // registry entry, no timer) — the row alone drives the kill after a restart.
    [Fact]
    public async Task Enforcer_DeadlineElapsedAfterRestart_StillKills()
    {
        await SeedRunAsync("run-restart", jobId: "feedbeef0001",
            cancelRequested: true, deadline: DateTimeOffset.UtcNow.AddMinutes(-5));

        // Fresh enforcer over the same DB — the restart scenario by construction.
        var enforced = await NewEnforcer().RunOnceAsync(CancellationToken.None);

        enforced.Should().Be(1);
        _spawner.Verify(s => s.TerminateAsync("feedbeef0001", It.IsAny<CancellationToken>()), Times.Once);
        _published.OfType<RunFinishedEvent>().Single().Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task Enforcer_DeadlineNotElapsed_LeavesRunAlone()
    {
        await SeedRunAsync("run-grace", jobId: "aaaa00000000",
            cancelRequested: true, deadline: DateTimeOffset.UtcNow.AddSeconds(20));

        var enforced = await NewEnforcer().RunOnceAsync(CancellationToken.None);

        enforced.Should().Be(0, "the cooperative token still owns the grace window");
        _spawner.Verify(s => s.TerminateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _published.Should().BeEmpty();
    }

    [Fact]
    public async Task Enforcer_TerminateThrows_RowStaysNonTerminal_ForRetry()
    {
        _spawner.Setup(s => s.TerminateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("k8s API down"));
        await SeedRunAsync("run-retry", jobId: "bbbb00000000",
            cancelRequested: true, deadline: DateTimeOffset.UtcNow.AddSeconds(-1));

        var enforced = await NewEnforcer().RunOnceAsync(CancellationToken.None);

        enforced.Should().Be(0, "finalizing before the kill lands would mark a still-billing pod cancelled");
        _published.Should().BeEmpty();
    }

    // p0348: a spawned run that outran the wall-time ceiling (never registered in
    // the in-memory watchdog) is flagged cancel-requested with a kill deadline by
    // the DB-backed scan, then enters the normal enforcement path.
    [Fact]
    public async Task Enforcer_WallTimeOverdueSpawnedRun_FlaggedForCancel()
    {
        _orchestrator = new OrchestratorGlobalConfig { MaxRunWallTimeSeconds = 60 };
        using (var ctx = new AgentSmithDbContext(Options()))
        {
            ctx.Runs.Add(new Run
            {
                Id = "run-overdue", Project = "p1", Pipeline = "fix-bug", TicketId = "42",
                Platform = "github", Status = "running",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-90), JobId = "dddd00000000",
            });
            await ctx.SaveChangesAsync();
        }

        // The grace has NOT yet elapsed on the fresh deadline, so no kill this pass —
        // but the run is now flagged and will be enforced on a later scan.
        var enforced = await NewEnforcer().RunOnceAsync(CancellationToken.None);

        enforced.Should().Be(0);
        _published.OfType<RunCancelRequestedEvent>()
            .Should().ContainSingle(e => e.RunId == "run-overdue" && e.Reason == "watchdog-wall-time");
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == "run-overdue");
        run.CancelRequested.Should().BeTrue();
        run.CancelReason.Should().Be("watchdog-wall-time");
        run.CancelDeadlineAt.Should().NotBeNull();
    }

    // A running run still within the ceiling is left alone.
    [Fact]
    public async Task Enforcer_RunWithinWallTime_NotFlagged()
    {
        _orchestrator = new OrchestratorGlobalConfig { MaxRunWallTimeSeconds = 1800 };
        using (var ctx = new AgentSmithDbContext(Options()))
        {
            ctx.Runs.Add(new Run
            {
                Id = "run-young", Project = "p1", Pipeline = "fix-bug", TicketId = "7",
                Platform = "github", Status = "running",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2), JobId = "eeee00000000",
            });
            await ctx.SaveChangesAsync();
        }

        await NewEnforcer().RunOnceAsync(CancellationToken.None);

        _published.Should().BeEmpty();
        using var check = new AgentSmithDbContext(Options());
        check.Runs.Single(r => r.Id == "run-young").CancelRequested.Should().BeFalse();
    }

    // p0357 (p0330b): a NULL deadline is due NOW — pre-p0357 the candidate query
    // excluded it and the run wedged in 'cancelling' forever (observed 46-min hang).
    [Fact]
    public async Task Enforcer_NullDeadline_StillEnforces()
    {
        await SeedRunAsync("run-nodeadline", jobId: "ffff00000000",
            cancelRequested: true, deadline: null);

        var enforced = await NewEnforcer().RunOnceAsync(CancellationToken.None);

        enforced.Should().Be(1);
        _spawner.Verify(s => s.TerminateAsync("ffff00000000", It.IsAny<CancellationToken>()), Times.Once);
        _published.OfType<RunFinishedEvent>().Single().Status.Should().Be("cancelled");
    }

    // p0357 (p0330b): terminate retries are bounded — past the retry window an
    // unkillable pod no longer blocks the finalize; the run reaches terminal.
    [Fact]
    public async Task Enforcer_UnkillablePod_FinalizesAfterBoundedRetries_NotStuck()
    {
        _spawner.Setup(s => s.TerminateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("job gone (404)"));
        await SeedRunAsync("run-unkillable", jobId: "dead00000000",
            cancelRequested: true,
            deadline: DateTimeOffset.UtcNow - CancelEnforcer.TerminateRetryWindow - TimeSpan.FromMinutes(1));

        var enforced = await NewEnforcer().RunOnceAsync(CancellationToken.None);

        enforced.Should().Be(1, "past the retry window the run must still reach terminal");
        _published.OfType<RunFinishedEvent>().Single().Status.Should().Be("cancelled");
        _ticketProvider.Verify(p => p.FinalizeAsync(
            new TicketId("42"), It.IsAny<string>(), "Rejected", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Projector_LateRunFinished_DoesNotOverwriteCancelled()
    {
        await SeedRunAsync("run-late", jobId: "cccc00000000",
            cancelRequested: true, deadline: DateTimeOffset.UtcNow.AddSeconds(-1));
        var cancelledAt = DateTimeOffset.UtcNow;
        await ApplyAsync(new RunFinishedEvent("run-late", "cancelled", null, "enforced", cancelledAt));

        // The killed pod's buffered success event arrives late.
        await ApplyAsync(new RunFinishedEvent(
            "run-late", "success", "https://pr", "done", cancelledAt.AddSeconds(5)));

        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == "run-late");
        run.Status.Should().Be("cancelled", "the FIRST terminal status is final");
        run.FinishedAt.Should().Be(cancelledAt);
    }

    [Fact]
    public async Task Projector_LateQueuedFinished_DoesNotReopenCancelled()
    {
        await SeedRunAsync("run-requeue", jobId: null,
            cancelRequested: true, deadline: DateTimeOffset.UtcNow.AddSeconds(-1));
        await ApplyAsync(new RunFinishedEvent("run-requeue", "cancelled", null, "enforced", DateTimeOffset.UtcNow));

        await ApplyAsync(new RunFinishedEvent(
            "run-requeue", "queued", null, "waiting for capacity", DateTimeOffset.UtcNow));

        using var check = new AgentSmithDbContext(Options());
        check.Runs.Single(r => r.Id == "run-requeue").Status.Should().Be("cancelled");
        check.QueuedTickets.Should().BeEmpty("a terminal run must not re-enter the capacity queue");
    }

    private CancelEnforcer NewEnforcer()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunRepository>();
        services.AddSingleton(_spawner.Object);
        // p0353: CancelEnforcer reads the wall-time LIVE from the loader each scan.
        // The stub returns the mutable _orchestrator so the wall-time test still lowers it.
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>()))
            .Returns(() => new AgentSmithConfig { Orchestrator = _orchestrator });
        services.AddSingleton(loader.Object);
        services.AddSingleton(new ServerContext("test.yml"));
        var provider = services.BuildServiceProvider();
        return new CancelEnforcer(
            provider, _events, _lease.Object, NewFinalizer(),
            TimeProvider.System, NullLogger<CancelEnforcer>.Instance);
    }

    // A high ceiling by default so the wall-time backstop stays inert in the
    // enforcement tests; the wall-time test lowers it.
    private OrchestratorGlobalConfig _orchestrator = new() { MaxRunWallTimeSeconds = 100_000 };

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
            factory.Object, loader.Object, _lease.Object, new ServerContext("config.yaml"),
            NullLogger<CancelledTicketFinalizer>.Instance);
    }

    private async Task SeedRunAsync(
        string runId, string? jobId, bool cancelRequested, DateTimeOffset? deadline)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "p1", Pipeline = "fix-bug", TicketId = "42",
            Platform = "github", Status = "running", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            CancelRequested = cancelRequested, CancelReason = "operator",
            CancelDeadlineAt = deadline, JobId = jobId,
        });
        await ctx.SaveChangesAsync();
    }

    private Task ApplyAsync(RunEvent ev) =>
        new RunEventApplier().ApplyAsync(new AgentSmithDbContext(Options()), ev, CancellationToken.None);

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
