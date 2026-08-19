using AgentSmith.Tests.TestSupport;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0327: the resume sweeper over a REAL migrated SQLite. Every scenario builds
/// a FRESH sweeper + stores over the same connection — the process-restart
/// shape by construction (nothing in-memory survives; the rows drive resume).
/// </summary>
public sealed class DialogueResumeSweeperTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-07-11T12:00:00Z");

    public DialogueResumeSweeperTests()
    {
        _connection = MigratedStoreTemplate.OpenCopy();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Answer_AfterProcessRestart_ResumesAtCheckpointedStep()
    {
        // Run parks: checkpoint + waiting_for_input landed before the "crash".
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(Checkpointed("run-1", deadline: T.AddDays(3)));
        await ApplyAsync(new RunFinishedEvent("run-1", "waiting_for_input", null, "Waiting", T));

        // The answer arrives while no worker exists — durable inbox row only.
        await BuildInbox().TryDeliverAsync("job-1",
            new DialogAnswer("q1", "approve", null, DateTimeOffset.UtcNow, "@op"), CancellationToken.None);

        // "Restart": a fresh sweeper over the same DB, nothing carried in memory.
        var resumed = await BuildSweeper().ScanOnceAsync(CancellationToken.None);

        resumed.Should().Be(1);
        using var ctx = new AgentSmithDbContext(Options());
        var entry = ctx.QueuedTickets.Single();
        entry.IsResume.Should().BeTrue();
        entry.ReservedRunId.Should().Be("run-1");
        entry.InitialContextJson.Should().Contain("ResumeCheckpoint");
        entry.InitialContextJson.Should().Contain("CheckoutSourceCommand",
            "the resume re-provisions the sandbox tree before re-entering the cursor");
        entry.InitialContextJson.Should().Contain("ApprovalCommand",
            "the cursor re-enters AT the asking step, which consumes the answer");
        ctx.RunCheckpoints.Single().ResumedAt.Should().NotBeNull();

        // Idempotent: a second scan (or a duplicate answer) enqueues nothing new.
        (await BuildSweeper().ScanOnceAsync(CancellationToken.None)).Should().Be(0);
        ctx.QueuedTickets.Count().Should().Be(1);
    }

    [Fact]
    public async Task Timeout_WhileCheckpointed_AppliesDefaultAnswerHeadless()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(Checkpointed("run-1", deadline: DateTimeOffset.UtcNow.AddMinutes(-1)));
        await ApplyAsync(new RunFinishedEvent("run-1", "waiting_for_input", null, "Waiting", T));

        var resumed = await BuildSweeper().ScanOnceAsync(CancellationToken.None);

        resumed.Should().Be(1);
        var answer = await BuildInbox().GetAsync("job-1", "q1", CancellationToken.None);
        answer!.AnsweredBy.Should().Be("system");
        answer.Comment.Should().Be("timeout");
        answer.Answer.Should().Be("reject", "the persisted DefaultAnswer applies headless at the deadline");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.QueuedTickets.Single().IsResume.Should().BeTrue();
    }

    [Fact]
    public async Task Sweeper_DeadlineNotReached_NoAnswer_LeavesCheckpointPending()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(Checkpointed("run-1", deadline: DateTimeOffset.UtcNow.AddDays(2)));
        await ApplyAsync(new RunFinishedEvent("run-1", "waiting_for_input", null, "Waiting", T));

        (await BuildSweeper().ScanOnceAsync(CancellationToken.None)).Should().Be(0);

        using var ctx = new AgentSmithDbContext(Options());
        ctx.RunCheckpoints.Single().ResumedAt.Should().BeNull();
        ctx.QueuedTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task Sweeper_RunStillUnwinding_DoesNotAbandonCheckpoint()
    {
        // The checkpoint event lands while the executor is still tearing down —
        // the run row says "running" until RunFinished(waiting_for_input) lands.
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(Checkpointed("run-1", deadline: DateTimeOffset.UtcNow.AddDays(2)));

        (await BuildSweeper().ScanOnceAsync(CancellationToken.None)).Should().Be(0);

        using var ctx = new AgentSmithDbContext(Options());
        ctx.RunCheckpoints.Single().ResumedAt.Should().BeNull(
            "the unwind gap must never consume a pending checkpoint");
    }

    [Fact]
    public async Task Sweeper_RunCancelledWhileParked_ConsumesWithoutResume()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(Checkpointed("run-1", deadline: DateTimeOffset.UtcNow.AddDays(2)));
        await ApplyAsync(new RunFinishedEvent("run-1", "cancelled", null, "Cancelled by operator", T));

        (await BuildSweeper().ScanOnceAsync(CancellationToken.None)).Should().Be(0);

        using var ctx = new AgentSmithDbContext(Options());
        ctx.RunCheckpoints.Single().ResumedAt.Should().NotBeNull("abandoned, never resumed");
        ctx.QueuedTickets.Should().BeEmpty();
    }

    /// <summary>
    /// p0457: the sweeper ASKS the ticket. Without this the collector could be perfect and
    /// still never run — the operator's reply would sit on the work item exactly as it did
    /// before, and every unit test around it would stay green.
    /// </summary>
    [Fact]
    public async Task AnAnswerOnTheTicket_ResumesTheRunAndMovesTheTicketOn()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(Checkpointed("run-1", deadline: DateTimeOffset.UtcNow.AddDays(2)));
        await ApplyAsync(new RunFinishedEvent("run-1", "waiting_for_input", null, "Waiting", T));
        var ticket = new AnsweringTicket(BuildInbox());

        var resumed = await BuildSweeper(ticket).ScanOnceAsync(CancellationToken.None);

        resumed.Should().Be(1, "an answer written on the ticket resumes the SAME run");
        ticket.MovedOn.Should().BeTrue(
            "the board must stop saying 'waiting for you' over a run that is working again");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.QueuedTickets.Single().IsResume.Should().BeTrue();
    }

    private DialogueResumeSweeper BuildSweeper(IParkedTicketDialogue? ticket = null)
    {
        var checkpoints = new DbRunCheckpointStore(ScopeFactory());
        var inbox = BuildInbox();
        var resumer = new RunResumer(
            new DbCapacityQueue(ScopeFactory()), checkpoints, NullLogger<RunResumer>.Instance);
        return new DialogueResumeSweeper(
            BuildProvider(), checkpoints, inbox, resumer, ticket ?? new SilentTicket(),
            TimeProvider.System, NullLogger<DialogueResumeSweeper>.Instance);
    }

    /// <summary>A work item carrying the operator's reply, delivered the way the poll does.</summary>
    private sealed class AnsweringTicket(IDialogueAnswerInbox inbox) : IParkedTicketDialogue
    {
        public bool MovedOn { get; private set; }

        public Task<bool> TryCollectAnswerAsync(RunCheckpointRecord checkpoint, CancellationToken ct) =>
            inbox.TryDeliverAsync(
                checkpoint.DialogueJobId,
                new DialogAnswer(checkpoint.QuestionId, "option-a", "ticket-comment", T, "jane"),
                ct);

        public Task MoveToInProgressAsync(RunCheckpointRecord checkpoint, CancellationToken ct)
        {
            MovedOn = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// p0457: these cases are about the DURABLE rows driving a resume. A checkpoint whose
    /// ticket says nothing is the shape they already assumed before the ticket could speak.
    /// </summary>
    private sealed class SilentTicket : IParkedTicketDialogue
    {
        public Task<bool> TryCollectAnswerAsync(RunCheckpointRecord checkpoint, CancellationToken ct) =>
            Task.FromResult(false);

        public Task MoveToInProgressAsync(RunCheckpointRecord checkpoint, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private IDialogueAnswerInbox BuildInbox() => new DbDialogueAnswerInbox(ScopeFactory());

    private IServiceScopeFactory ScopeFactory() =>
        BuildProvider().GetRequiredService<IServiceScopeFactory>();

    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<RunCheckpointRepository>();
        services.AddScoped<DialogueAnswerRepository>();
        services.AddScoped<QueuedTicketRepository>();
        services.AddScoped<RunRepository>();
        return services.BuildServiceProvider();
    }

    private static RunStartedEvent Started(string runId) => new(
        runId, "ticket", "fix-bug", ["repo-a"], T, "claude", "42",
        Project: "p1", Platform: "github");

    private static RunCheckpointedEvent Checkpointed(string runId, DateTimeOffset deadline) => new(
        runId, "p1", "42", "github", "fix-bug", "job-1", "q1",
        QuestionJson: """{"QuestionId":"q1","Type":3,"Text":"Approve?","Context":null,"Choices":null,"DefaultAnswer":"reject","Timeout":"3.00:00:00"}""",
        RemainingCommandsJson: """[{"Name":"CheckoutSourceCommand"},{"Name":"ApprovalCommand"},{"Name":"AgenticMasterCommand"}]""",
        ContextJson: "[]", ExecutionCount: 14,
        AskedAt: T, AnswerDeadlineAt: deadline, Timestamp: T);

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(RunEvent ev) =>
        await new RunEventApplier(new(), new(), new(), new(), new(), new(), new()).ApplyAsync(new AgentSmithDbContext(Options()), ev, CancellationToken.None);
}
