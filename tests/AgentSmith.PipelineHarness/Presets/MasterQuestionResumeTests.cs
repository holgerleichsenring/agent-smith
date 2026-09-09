using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-03-3c07: a run parked on the MASTER's mid-run question resumes and continues,
/// with the operator's answer reaching the model that asked — LLM-free, through the real
/// composition and the same seams production uses.
/// <para>
/// MasterAskHumanParkTests proves the door out (the question reaches the ticket and the run
/// parks); DurableDialogueTests proves the way back for the dialogue GATE. Nothing proved the
/// way back for the master's park, which is how a path that never worked stayed green: on
/// live run 2026-09-08T13-28-32-a109 the operator answered in the dashboard, the run
/// relaunched, checked out its repo — and parked again with no question pending, because
/// the master's park marker had been captured by its own checkpoint and restored.
/// </para>
/// <para>
/// The answer is posted through <see cref="IDialogueTransport"/>, the seam the
/// <c>/api/runs/{id}/answer</c> endpoint uses; the sweeper turns it into a resume; the real
/// pump launches it; the resumed worker re-enters at the cursor.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class MasterQuestionResumeTests
{
    private const string Fixture = "agentsmith-dialogue.yml";
    private const string Project = "fixture-fix-bug";
    private const string Pipeline = "fix-bug";
    private const string TicketNumber = "1";
    private const string Question = "Should the refresh window be 5 or 15 minutes?";
    private const string Answer = "15 minutes: the session must survive one full refresh.";
    private const string ParkStatus = "Question";

    private const string GreenVerdict =
        """Done per the operator's answer. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"refresh window set to 15 minutes","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""";

    [Fact]
    public async Task MasterAsks_OperatorAnswers_TheRunResumesIntoTheMasterAndFinishes()
    {
        var dbPath = NewDbPath();
        var jobQueue = new RecordingJobQueue();
        var tickets = new RecordingTicketProvider();
        try
        {
            // ---- Act 1: the master asks, the run parks ----
            var runId = await ParkAsync(dbPath, jobQueue, tickets);
            var checkpoint = SingleCheckpoint(dbPath);
            tickets.Finalized.Should().ContainSingle().Which.Status.Should().Be(ParkStatus);

            // ---- Act 2: the operator answers in a fresh composition, the run relaunches ----
            await using var second = BuildHarness(dbPath, jobQueue, tickets);
            IReadOnlyList<ChatMessage>? masterPrompt = null;
            second.ChatClient
                .EnqueueDeferred(() =>
                {
                    masterPrompt = second.ChatClient.LastMessages;
                    return WriteFile("csharp-fixture/src/Patch.cs", "// refresh window: 15 minutes");
                })
                .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"csharp-fixture"}""")
                .EnqueueToolCall("update_progress", """{"items":[{"id":"guard","activity":"Answer an empty request body with 400","status":"done"}]}""")
                .EnqueueText(GreenVerdict);
            await AnswerAsync(second, checkpoint, Answer);
            (await Sweeper(second).ScanOnceAsync(CancellationToken.None)).Should().Be(1,
                "the answered checkpoint enqueues exactly one resume");
            await DurableDialogueHarness.BuildPump(second, Fixture, jobQueue)
                .TickAsync(CancellationToken.None);
            var resumeRequest = jobQueue.DequeueViaJsonRoundTrip();
            resumeRequest.RunId.Should().Be(runId, "the resume reuses the reserved run row");

            // ---- Act 3: the resumed worker re-enters at the cursor ----
            var resumed = await DurableDialogueHarness.ExecuteAsync(second, Fixture, resumeRequest);

            // ---- Assert: not parked again, the master saw the answer, the run finished ----
            using var ctx = Db(dbPath);
            var run = ctx.Runs.Single(r => r.Id == runId);
            run.Status.Should().NotBe("waiting_for_input",
                "the restored run must not be parked again by its own park marker");
            masterPrompt.Should().NotBeNull("the answer must re-engage the master that asked");
            string.Join("\n", masterPrompt!.Select(m => m.Text ?? string.Empty))
                .Should().Contain(Answer, "the operator's answer must be in the resumed master's input");
            StepNames(ctx, runId).Should().Contain(CommandNames.CommitPhaseWork,
                "the run must continue past the question step");
            tickets.Finalized.Count(f => f.Status == ParkStatus).Should().Be(1,
                "the run must not re-post the same question on resume");
            ctx.RunCheckpoints.Where(c => c.RunId == runId).Should().ContainSingle(
                "the resumed leg must not write a second checkpoint for the answered question")
                .Which.ResumedAt.Should().NotBeNull();
            resumed.IsSuccess.Should().BeTrue($"the resumed run must complete: {resumed.Message}");
            run.FinishedAt.Should().NotBeNull("the run is over once its remaining commands ran");
            run.Status.Should().Be("success");
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    // ---- acts ----

    private static async Task<string> ParkAsync(
        string dbPath, RecordingJobQueue jobQueue, RecordingTicketProvider tickets)
    {
        string runId;
        await using (var first = BuildHarness(dbPath, jobQueue, tickets))
        {
            await DurableDialogueHarness.MigrateAsync(first);
            first.ChatClient
                // p0390: DeriveSpecification runs before the master and drains one FIFO slot.
                .EnqueueText(SpecDerivationFixture.DerivationJson)
                .EnqueueToolCall("ask_human", $$"""{"question":"{{Question}}"}""")
                .EnqueueText("Waiting for the operator's answer.");
            var result = await DurableDialogueHarness.ExecuteAsync(first, Fixture, Request());
            result.IsSuccess.Should().BeTrue("a clarification park is an incomplete run, not a failure");
            result.Message.Should().Contain("awaiting_user_input");
            runId = SingleRun(dbPath).Id;
        }

        AssertParked(dbPath);
        return runId;
    }

    private static void AssertParked(string dbPath)
    {
        using var ctx = Db(dbPath);
        var run = ctx.Runs.Single();
        run.Status.Should().Be("waiting_for_input");
        run.FinishedAt.Should().BeNull("waiting is an active state — the run is NOT over");
        var checkpoint = ctx.RunCheckpoints.Single();
        checkpoint.ResumedAt.Should().BeNull();
        checkpoint.QuestionJson.Should().Contain(Question);
        checkpoint.RemainingCommandsJson.Should().Contain(CommandNames.CheckoutSource,
            "the resume re-provisions the working tree first — sandboxes are cattle");
        checkpoint.RemainingCommandsJson.Should().Contain(CommandNames.MasterOpenQuestions,
            "the cursor re-enters the asking step so it consumes the answer");
    }

    // ---- plumbing ----

    private static RealCompositionHarness BuildHarness(
        string dbPath, RecordingJobQueue jobQueue, RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(
            FixturePaths.For(Fixture), SandboxBackend.Stub, session: null,
            SkillsBackend.Fixture, services =>
            {
                DurableDialogueHarness.RegisterDurableSpine(services, dbPath, jobQueue);
                // The production LLM-driven analyzer would drain the scripted FIFO at AnalyzeCode.
                HarnessProjectAnalyzerStub.Register(services);
                services.RemoveAll<ITicketProviderFactory>();
                services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
            });

    private static ChatResponse WriteFile(string path, string content) =>
        new(new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("call_resume_write", "write_file",
                new Dictionary<string, object?> { ["path"] = path, ["content"] = content }),
        ]));

    private static DialogueResumeSweeper Sweeper(RealCompositionHarness harness) =>
        harness.Services.GetRequiredService<DialogueResumeSweeper>();

    // The seam RunControlEndpoints.AnswerAsync uses: durable inbox first, hot stream second.
    private static Task AnswerAsync(
        RealCompositionHarness harness, RunCheckpoint checkpoint, string answer) =>
        harness.Services.GetRequiredService<IDialogueTransport>().PublishAnswerAsync(
            checkpoint.DialogueJobId,
            new DialogAnswer(checkpoint.QuestionId, answer, null, DateTimeOffset.UtcNow, "dashboard-operator"),
            CancellationToken.None);

    private static IReadOnlyList<string> StepNames(AgentSmithDbContext ctx, string runId) =>
        ctx.RunSteps.Where(s => s.RunId == runId).Select(s => s.CommandName ?? string.Empty).ToList();

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

    // The trigger seeds the park status into the run context in production
    // (SpawnRequestBuilder); the request carries it here the same way.
    private static PipelineRequest Request() => new(
        Project, Pipeline, TicketId: new TicketId(TicketNumber), Headless: true,
        Context: new Dictionary<string, object>
        {
            [ContextKeys.NeedsClarificationStatus] = ParkStatus,
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
