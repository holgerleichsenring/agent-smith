using AgentSmith.Contracts.Models;
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
/// is terminal-only. The tracker ticket is never touched.
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
        _lease.Verify(l => l.ReleaseAsync("p1", new TicketId("42"), It.IsAny<CancellationToken>()), Times.Once);
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
        _lease.Verify(l => l.ReleaseAsync(It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<CancellationToken>()),
            Times.Never, "a failed kill must not release the lease and orphan a live pod");
        using var db = new AgentSmithDbContext(Options());
        db.Runs.Should().ContainSingle(r => r.Id == "run-stuck");
    }

    [Fact]
    public async Task Delete_QueuedRun_RemovesQueueEntryAndReservation_ThenDeletes()
    {
        var reserved = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "42", "fix-bug", "github",
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

    [Fact]
    public async Task Delete_LeavesTrackerTicketUntouched()
    {
        var reserved = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "42", "fix-bug", "github",
            "2026-07-14T10-00-00-c3d4", "waiting for sandbox capacity",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        await NewDeleter().DeleteAsync(reserved, CancellationToken.None);

        // Cancel terminalizes the ticket; delete must NOT — it is pure record cleanup.
        _ticketProvider.Verify(p => p.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BulkDelete_ClearsTerminalOnly_LeavesRunningAndQueued()
    {
        await SeedTerminalRunWithSatellitesAsync("run-terminal");
        await SeedRunningRunAsync("run-running", jobId: "cccc00000000");
        var queued = await _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", "99", "fix-bug", "github",
            "2026-07-14T10-00-00-e5f6", "waiting", ["repo-a"],
            InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

        var deleted = await NewDeleter().DeleteTerminalAsync(CancellationToken.None);

        deleted.Should().Be(1);
        _spawner.Verify(s => s.TerminateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "bulk clear is terminal-only and never force-kills");
        using var db = new AgentSmithDbContext(Options());
        db.Runs.Select(r => r.Id).Should().BeEquivalentTo(new[] { "run-running", queued });
    }

    private RunDeleter NewDeleter()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_spawner.Object);
        var provider = services.BuildServiceProvider();
        var uow = new AgentSmithDbContext(Options());
        return new RunDeleter(
            provider, new RunRepository(uow), new RunDeletionRepository(uow),
            _lease.Object, _queue, NullLogger<RunDeleter>.Instance);
    }

    private async Task SeedRunningRunAsync(string runId, string jobId)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "p1", Pipeline = "fix-bug", TicketId = "42",
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
            Id = runId, Project = "p1", Pipeline = "fix-bug", TicketId = "7",
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
            Project = "p1", TicketId = "7", Pipeline = "fix-bug", Platform = "github",
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
