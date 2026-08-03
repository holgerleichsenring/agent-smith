using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0327/p0393b: the durable-dialogue proof, LLM-free through the REAL
/// composition. A ticket run's ask crosses the (zero-width fixture) hot window,
/// checkpoints, and parks as waiting_for_input; the orchestrator "restarts"
/// (first harness disposed, a second built over the same SQLite file); the
/// operator's answer lands in the durable inbox; the sweeper turns it into a
/// capacity-queue resume entry; the REAL pump launches it; and the resumed
/// request completes the run — ONE run record, correct result.
///
/// <para>
/// p0393b — why the subject is a ticket-triggered discussion run and NOT
/// spec-dialog, which the phase spec named:
/// </para>
/// <list type="bullet">
/// <item>Durable dialogue is ticket-keyed by construction. DialogueAskGate's
/// eligibility check requires ContextKeys.TicketId, DialogueCheckpointWriter
/// refuses to write without one, RunCheckpointedEvent carries the ticket id, and
/// RunResumer relaunches through the capacity queue keyed on (project, ticket).</item>
/// <item>A spec-dialog turn has no ticket. It runs IN PROCESS from a live chat
/// thread (SpecDialogTurnRunner owns its sandboxes and a SpecDialogReplySlot that
/// is explicitly excluded from the checkpoint), so a resume launched into the job
/// queue could neither reconstitute the slot nor reach the thread. Its operator is
/// present by construction — that is why the ask gate names spec-dialog as a
/// full-hot-wait surface, not a parking one.</item>
/// <item>Since p0393 deleted Approval and p0393a removed NegotiateExpectation, no
/// shipped preset declares a step-level ask at all — the spine has no production
/// carrier today. The harness supplies the park point (DurableDialogueHarness
/// binds the production AskContextBuilder to the preset's LoadContext slot), the
/// same way it supplies the LLM script; every other part of the spine is the real
/// registration. That gap is recorded in decisions/p0393b.yaml.</item>
/// </list>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DurableDialogueTests
{
    private const string Fixture = "agentsmith-dialogue.yml";
    private const string Project = "fixture-durable-dialogue";
    private const string Pipeline = "mad-discussion";
    private const string TicketNumber = "7";
    private const string QuestionId = "durable-dialogue-q1";

    [Fact]
    public async Task TicketRun_CheckpointMidAsk_RestartAnswerResume_OneRunRecord()
    {
        var dbPath = NewDbPath();
        var jobQueue = new RecordingJobQueue();
        try
        {
            // ---- Act 1: the run parks at the ask ----
            var runId = await ParkAsync(dbPath, jobQueue);
            var checkpoint = SingleCheckpoint(dbPath);

            // ---- Act 2: "restart" — a fresh composition over the same DB ----
            await using var second = DurableDialogueHarness.Build(Fixture, dbPath, jobQueue);

            // ---- Act 3: the operator answers AFTER the restart — durable inbox first ----
            second.ChatClient.EnqueueText("Discussion synthesised.");
            await AnswerAsync(second, checkpoint, "keep the current design");
            (await Sweeper(second).ScanOnceAsync(CancellationToken.None)).Should().Be(1);
            await DurableDialogueHarness.BuildPump(second, Fixture, jobQueue)
                .TickAsync(CancellationToken.None);

            var resumeRequest = jobQueue.DequeueViaJsonRoundTrip();
            resumeRequest.RunId.Should().Be(runId, "the resume reuses the reserved run row");
            resumeRequest.Context.Should().ContainKey(ContextKeys.ResumeCheckpoint);

            // ---- Act 4: the resumed worker re-enters at the cursor ----
            var resumed = await DurableDialogueHarness.ExecuteAsync(second, Fixture, resumeRequest);

            resumed.IsSuccess.Should().BeTrue($"the resumed run must complete: {resumed.Message}");
            second.StubSandboxFactory!.Spawned.Should().NotBeEmpty(
                "resume re-provisions fresh sandboxes — the checkpointed run held none");
            AssertOneCompletedRun(dbPath, runId);
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    [Fact]
    public async Task AnswerArrivesAfterRestart_DurableInboxDeliversIt()
    {
        var dbPath = NewDbPath();
        var jobQueue = new RecordingJobQueue();
        try
        {
            await ParkAsync(dbPath, jobQueue);
            var checkpoint = SingleCheckpoint(dbPath);

            // The composition that PARKED is gone; a different one takes the answer.
            await using var second = DurableDialogueHarness.Build(Fixture, dbPath, jobQueue);
            await AnswerAsync(second, checkpoint, "the answer the operator typed hours later");

            // And a THIRD composition reads it back: the answer outlived both the
            // process that asked and the process that received it, so it is the
            // durable inbox holding it, not any in-memory hot wait.
            await using var third = DurableDialogueHarness.Build(Fixture, dbPath, jobQueue);
            var delivered = await third.Services.GetRequiredService<IDialogueAnswerInbox>()
                .GetAsync(checkpoint.DialogueJobId, checkpoint.QuestionId, CancellationToken.None);

            delivered.Should().NotBeNull("the durable inbox must survive the restart");
            delivered!.Answer.Should().Be("the answer the operator typed hours later");
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    [Fact]
    public async Task ResumeSweeper_ParkedRunWithAnswer_ResumesExactlyOnce()
    {
        var dbPath = NewDbPath();
        var jobQueue = new RecordingJobQueue();
        try
        {
            await ParkAsync(dbPath, jobQueue);
            var checkpoint = SingleCheckpoint(dbPath);

            await using var second = DurableDialogueHarness.Build(Fixture, dbPath, jobQueue);
            await AnswerAsync(second, checkpoint, "approve");

            var sweeper = Sweeper(second);
            (await sweeper.ScanOnceAsync(CancellationToken.None)).Should().Be(1,
                "the answered checkpoint enqueues exactly one resume");
            (await sweeper.ScanOnceAsync(CancellationToken.None)).Should().Be(0,
                "a consumed checkpoint must never enqueue a second resume");

            using var ctx = Db(dbPath);
            ctx.RunCheckpoints.Single().ResumedAt.Should().NotBeNull(
                "the checkpoint is marked consumed by the scan that enqueued it");
            ctx.QueuedTickets.Should().ContainSingle(
                "the capacity queue carries exactly one resume entry");
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    // ---- acts ----

    // Act 1 for every scenario: a ticket run reaches the ask, the (zero-width)
    // hot window elapses, the run checkpoints and parks. Returns the run id.
    private static async Task<string> ParkAsync(string dbPath, RecordingJobQueue jobQueue)
    {
        string runId;
        await using (var first = DurableDialogueHarness.Build(Fixture, dbPath, jobQueue))
        {
            await DurableDialogueHarness.MigrateAsync(first);
            var result = await DurableDialogueHarness.ExecuteAsync(first, Fixture, Request());
            result.IsSuccess.Should().BeTrue("parking is a clean halt, not a failure");
            runId = SingleRun(dbPath).Id;
        }

        AssertParked(dbPath);
        return runId;
    }

    // ---- assertions ----

    private static void AssertParked(string dbPath)
    {
        using var ctx = Db(dbPath);
        var run = ctx.Runs.Single();
        run.Status.Should().Be("waiting_for_input");
        run.FinishedAt.Should().BeNull("waiting is an active state — the run is NOT over");
        var checkpoint = ctx.RunCheckpoints.Single();
        checkpoint.ResumedAt.Should().BeNull();
        checkpoint.QuestionId.Should().Be(QuestionId);
        checkpoint.RemainingCommandsJson.Should().Contain(CommandNames.CheckoutSource,
            "the resume re-provisions the working tree first — sandboxes are cattle");
        checkpoint.RemainingCommandsJson.Should().Contain(DurableDialogueHarness.ParkStep,
            "the cursor re-enters the asking step so it consumes the answer");
    }

    private static void AssertOneCompletedRun(string dbPath, string runId)
    {
        using var ctx = Db(dbPath);
        var run = ctx.Runs.Single(); // checkpoint/resume must never mint a second run row
        run.Id.Should().Be(runId);
        run.Status.Should().Be("success");
        run.FinishedAt.Should().NotBeNull();
        ctx.RunCheckpoints.Single().ResumedAt.Should().NotBeNull();
        ctx.QueuedTickets.Should().BeEmpty("the launched resume entry is consumed");
    }

    // ---- plumbing ----

    private static DialogueResumeSweeper Sweeper(RealCompositionHarness harness) =>
        harness.Services.GetRequiredService<DialogueResumeSweeper>();

    private static Task AnswerAsync(
        RealCompositionHarness harness, RunCheckpoint checkpoint, string answer) =>
        harness.Services.GetRequiredService<IDialogueTransport>().PublishAnswerAsync(
            checkpoint.DialogueJobId,
            new DialogAnswer(checkpoint.QuestionId, answer, null, DateTimeOffset.UtcNow, "@operator"),
            CancellationToken.None);

    private static RunCheckpoint SingleCheckpoint(string dbPath)
    {
        using var ctx = Db(dbPath);
        return ctx.RunCheckpoints.Single();
    }

    private static Run SingleRun(string dbPath)
    {
        using var ctx = Db(dbPath);
        return ctx.Runs.Single();
    }

    // Headless=false so the ask actually asks — the checkpointable shape. The
    // question's timeout is days-scale against the fixture's zero-width hot
    // window, which is exactly the "outlives the process" case.
    private static PipelineRequest Request() => new(
        Project, Pipeline, TicketId: new TicketId(TicketNumber), Headless: false,
        Context: new Dictionary<string, object>
        {
            [ContextKeys.DialogueQuestion] = new DialogQuestion(
                QuestionId, QuestionType.FreeText,
                "Which direction should the discussion take?",
                Context: null, Choices: null, DefaultAnswer: "proceed",
                Timeout: TimeSpan.FromDays(3)),
        });

    private static string NewDbPath() =>
        Path.Combine(Path.GetTempPath(), $"agentsmith-harness-{Guid.NewGuid():N}.db");

    private static void Cleanup(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
            if (File.Exists(f)) try { File.Delete(f); } catch (IOException) { /* best-effort */ }
    }

    private static AgentSmithDbContext Db(string dbPath) => new(
        new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlite($"Data Source={dbPath}").Options);
}
