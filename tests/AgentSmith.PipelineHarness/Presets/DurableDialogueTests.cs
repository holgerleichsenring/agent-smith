using AgentSmith.Contracts.Dialogue;
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
/// p0327: the durable-dialogue proof, LLM-free through the REAL composition.
/// A fix-bug run with an interactive approval crosses the (zero-width fixture)
/// hot window, checkpoints, and parks as waiting_for_input; the orchestrator
/// "restarts" (first harness disposed, a second built over the same SQLite
/// file); the operator's answer lands in the durable inbox; the sweeper turns
/// it into a capacity-queue resume entry; the REAL pump launches it; and the
/// resumed request completes the run — ONE run record, correct result.
/// p0328 note: fix-bug now negotiates the expectation BEFORE planning, so the
/// FIRST interactive park is the ratification ask (approved verbatim here);
/// the approval this test was built around is the SECOND park — the sequential
/// checkpoint upsert (one row per run, re-parked) is proven en route.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DurableDialogueTests
{
    private const string Fixture = "agentsmith-dialogue.yml";
    private const string Project = "fixture-fix-bug";
    private const string TicketNumber = "7";

    [Fact]
    public async Task FixBug_CheckpointMidApproval_RestartAnswerResume_OneRunRecord()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"agentsmith-harness-{Guid.NewGuid():N}.db");
        var jobQueue = new RecordingJobQueue();
        try
        {
            // ---- Act 1: the run parks at the expectation ratification (p0328,
            // the first interactive ask of the preset) ----
            string runId;
            await using (var first = DurableDialogueHarness.Build(Fixture, dbPath, jobQueue))
            {
                await DurableDialogueHarness.MigrateAsync(first);
                // Call 1 is AnalyzeCode's project analyzer (not stubbed in the
                // harness); call 2 is the p0328 expectation-drafting call.
                first.ChatClient.EnqueueText("{}").EnqueueText(ExpectationNegotiationTests.DraftJson);
                var result = await DurableDialogueHarness.ExecuteAsync(first, Fixture, Request(runId: null));
                result.IsSuccess.Should().BeTrue("parking is a clean halt, not a failure");
                runId = SingleRun(dbPath).Id;
            }

            AssertParked(dbPath, expectQuestion: "Ratify");
            var ratification = SingleCheckpoint(dbPath);

            // ---- Act 2: "restart" — a fresh composition over the same DB;
            // approve the expectation verbatim; the run then parks at Approval ----
            await using var second = DurableDialogueHarness.Build(Fixture, dbPath, jobQueue);
            await AnswerAsync(second, ratification, "approve");
            (await second.Services.GetRequiredService<DialogueResumeSweeper>()
                .ScanOnceAsync(CancellationToken.None)).Should().Be(1);
            await DurableDialogueHarness.BuildPump(second, Fixture, jobQueue).TickAsync(CancellationToken.None);
            var afterRatify = await DurableDialogueHarness.ExecuteAsync(
                second, Fixture, jobQueue.DequeueViaJsonRoundTrip());
            afterRatify.IsSuccess.Should().BeTrue("the ratified run parks cleanly at the approval");

            AssertParked(dbPath, expectQuestion: "Approve");
            var approval = SingleCheckpoint(dbPath);

            // ---- Act 3: the operator approves AFTER the restart — durable inbox first ----
            second.ChatClient
                .EnqueueToolCall("write_file", """{"path":"csharp-fixture/src/Patch.cs","content":"// fix"}""")
                .EnqueueText("""Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"patched"}""");
            await AnswerAsync(second, approval, "approve");
            (await second.Services.GetRequiredService<DialogueResumeSweeper>()
                .ScanOnceAsync(CancellationToken.None)).Should().Be(1);
            await DurableDialogueHarness.BuildPump(second, Fixture, jobQueue).TickAsync(CancellationToken.None);

            var resumeRequest = jobQueue.DequeueViaJsonRoundTrip();
            resumeRequest.RunId.Should().Be(runId, "the resume reuses the reserved run row");
            resumeRequest.Context.Should().ContainKey("ResumeCheckpoint");

            // ---- Act 4: the resumed worker re-enters at the cursor ----
            var resumed = await DurableDialogueHarness.ExecuteAsync(second, Fixture, resumeRequest);

            resumed.IsSuccess.Should().BeTrue("the resumed run must complete");
            second.StubSandboxFactory!.Spawned.Should().NotBeEmpty(
                "resume re-provisions fresh sandboxes — the checkpointed run held none");
            AssertOneCompletedRun(dbPath, runId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    // ---- assertions ----

    private static void AssertParked(string dbPath, string expectQuestion)
    {
        using var ctx = Db(dbPath);
        var run = ctx.Runs.Single();
        run.Status.Should().Be("waiting_for_input");
        run.FinishedAt.Should().BeNull("waiting is an active state — the run is NOT over");
        var checkpoint = ctx.RunCheckpoints.Single();
        checkpoint.ResumedAt.Should().BeNull();
        checkpoint.QuestionJson.Should().Contain(expectQuestion);
        checkpoint.RemainingCommandsJson.Should().Contain("CheckoutSourceCommand",
            "the resume re-provisions the working tree first — sandboxes are cattle");
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

    // ---- plumbing shared with ExpectationNegotiationTests (p0328) ----

    internal static Task AnswerAsync(
        RealCompositionHarness harness, RunCheckpoint checkpoint, string answer) =>
        harness.Services.GetRequiredService<IDialogueTransport>().PublishAnswerAsync(
            checkpoint.DialogueJobId,
            new DialogAnswer(checkpoint.QuestionId, answer, null, DateTimeOffset.UtcNow, "@operator"),
            CancellationToken.None);

    internal static RunCheckpoint SingleCheckpoint(string dbPath)
    {
        using var ctx = Db(dbPath);
        return ctx.RunCheckpoints.Single();
    }

    internal static Run SingleRun(string dbPath)
    {
        using var ctx = Db(dbPath);
        return ctx.Runs.Single();
    }

    // Headless=false so the asks actually ask — the checkpointable shape.
    internal static PipelineRequest Request(string? runId) => new(
        Project, "fix-bug", TicketId: new TicketId(TicketNumber), Headless: false, RunId: runId);

    internal static AgentSmithDbContext Db(string dbPath) => new(
        new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlite($"Data Source={dbPath}").Options);
}
