using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0327: the projector side of durable dialogue. RunCheckpointed upserts the
/// checkpoint row; RunFinished(waiting_for_input) keeps the row in the active
/// set like 'queued'; RunStarted on a waiting row promotes it to running (the
/// resume launches on the SAME row); the answer inbox is first-wins.
/// </summary>
public sealed class DurableDialogueProjectionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-07-11T12:00:00Z");

    public DurableDialogueProjectionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Projector_RunCheckpointed_PersistsCheckpointRow()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(Checkpointed("run-1"));

        using var ctx = new AgentSmithDbContext(Options());
        var row = ctx.RunCheckpoints.Single();
        row.RunId.Should().Be("run-1");
        row.Project.Should().Be("p1");
        row.TicketId.Should().Be("42");
        row.Pipeline.Should().Be("fix-bug");
        row.DialogueJobId.Should().Be("job-1");
        row.QuestionId.Should().Be("q1");
        row.RemainingCommandsJson.Should().Contain("ApprovalCommand");
        row.ExecutionCount.Should().Be(14);
        row.ResumedAt.Should().BeNull("the checkpoint is pending until an answer arrives");
    }

    [Fact]
    public async Task Projector_WaitingForInputFinished_KeepsRowActive_NoQueueBackstop()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(Checkpointed("run-1"));
        await ApplyAsync(new RunFinishedEvent(
            "run-1", "waiting_for_input", null, "Waiting for an operator answer", T));

        using var ctx = new AgentSmithDbContext(Options());
        var run = ctx.Runs.Single();
        run.Status.Should().Be("waiting_for_input");
        run.FinishedAt.Should().BeNull("waiting_for_input is a WAITING state like queued");
        ctx.QueuedTickets.Should().BeEmpty(
            "the resume sweeper owns the queue entry — the TOCTOU backstop is for capacity only");
    }

    [Fact]
    public async Task Projector_RunStartedOnWaitingRow_PromotesToRunning_SameRow()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(new RunFinishedEvent("run-1", "waiting_for_input", null, "Waiting", T));

        await ApplyAsync(Started("run-1")); // the resume launch, same reserved id

        using var ctx = new AgentSmithDbContext(Options());
        var run = ctx.Runs.Single();
        run.Status.Should().Be("running");
        run.FinishedAt.Should().BeNull();
        ctx.RunRepos.Count(r => r.RunId == "run-1").Should().Be(1);
    }

    [Fact]
    public async Task Inbox_DuplicateAnswer_FirstWins()
    {
        var repo = new DialogueAnswerRepository(
            new AgentSmithDbContext(Options()), new SqliteUniqueViolationTranslator());
        var first = new DialogAnswer("q1", "approve", null, T, "@first");

        (await repo.TryDeliverAsync("job-1", first, CancellationToken.None)).Should().BeTrue();

        var second = new DialogueAnswerRepository(
            new AgentSmithDbContext(Options()), new SqliteUniqueViolationTranslator());
        var duplicate = new DialogAnswer("q1", "reject", null, T.AddMinutes(1), "@second");
        (await second.TryDeliverAsync("job-1", duplicate, CancellationToken.None))
            .Should().BeFalse("the unique index makes first-answer-wins a database guarantee");

        var stored = await new DialogueAnswerRepository(
                new AgentSmithDbContext(Options()), new SqliteUniqueViolationTranslator())
            .GetAsync("job-1", "q1", CancellationToken.None);
        stored!.Answer.Should().Be("approve");
        stored.AnsweredBy.Should().Be("@first");
    }

    [Fact]
    public async Task CheckpointStore_TryMarkResumed_ExactlyOneWriterWins()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(Checkpointed("run-1"));

        var repo = new RunCheckpointRepository(new AgentSmithDbContext(Options()));
        (await repo.TryMarkResumedAsync("run-1", T, CancellationToken.None)).Should().BeTrue();
        (await repo.TryMarkResumedAsync("run-1", T, CancellationToken.None))
            .Should().BeFalse("the guarded update consumes a pending checkpoint exactly once");
        (await repo.ListPendingAsync(CancellationToken.None)).Should().BeEmpty();
    }

    private static RunStartedEvent Started(string runId) => new(
        runId, "ticket", "fix-bug", ["repo-a"], T, "claude", "42",
        Project: "p1", Platform: "github");

    private static RunCheckpointedEvent Checkpointed(string runId) => new(
        runId, "p1", "42", "github", "fix-bug", "job-1", "q1",
        QuestionJson: """{"QuestionId":"q1","Type":3,"Text":"Approve?","Context":null,"Choices":null,"DefaultAnswer":"reject","Timeout":"3.00:00:00"}""",
        RemainingCommandsJson: """[{"Name":"CheckoutSourceCommand"},{"Name":"ApprovalCommand"},{"Name":"AgenticMasterCommand"}]""",
        ContextJson: "[]", ExecutionCount: 14,
        AskedAt: T, AnswerDeadlineAt: T.AddDays(3), Timestamp: T);

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(RunEvent ev) =>
        await new RunEventApplier().ApplyAsync(new AgentSmithDbContext(Options()), ev, CancellationToken.None);
}
