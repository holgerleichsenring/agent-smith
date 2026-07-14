using AgentSmith.Contracts.Expectations;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0328: the expectation-negotiation proof, LLM-free through the REAL
/// composition. A fix-bug run drafts the Soll block (one scripted LLM call),
/// parks on the durable ratification ask; the orchestrator restarts; the
/// operator's EDITED block lands in the durable inbox; the resumed run parses
/// the edit back into the schema WITHOUT re-drafting (no LLM call), records
/// outcome=edited + edit distance on the RunExpectation row, and carries the
/// ratified assertions into the rest of the run (second park at Approval,
/// approved, run completes — one run record).
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ExpectationNegotiationTests
{
    private const string Fixture = "agentsmith-dialogue.yml";

    internal const string DraftJson = """
        {"observed": "The endpoint returns 500 on empty payloads.",
         "expected": ["Empty payloads return 400.", "Existing callers stay unaffected."],
         "constraints": ["No new dependencies."],
         "open_question": null}
        """;

    private const string EditedAnswer = """
        ## Expected
        - [ ] Empty payloads return 422.
        - [ ] Existing callers stay unaffected.

        ## Constraints
        - No new dependencies.
        """;

    [Fact]
    public async Task FixBug_NegotiateEditResume_RatifiedEditBecomesAcceptanceContract()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"agentsmith-harness-{Guid.NewGuid():N}.db");
        var jobQueue = new RecordingJobQueue();
        try
        {
            // ---- Act 1: draft → post → park on the ratification ask ----
            string runId;
            await using (var first = DurableDialogueHarness.Build(Fixture, dbPath, jobQueue))
            {
                await DurableDialogueHarness.MigrateAsync(first);
                // Call 1 is AnalyzeCode's project analyzer (not stubbed in the
                // harness); call 2 is the drafting call.
                first.ChatClient.EnqueueText("{}").EnqueueText(DraftJson);
                var result = await DurableDialogueHarness.ExecuteAsync(
                    first, Fixture, DurableDialogueTests.Request(runId: null));
                result.IsSuccess.Should().BeTrue(
                    "parking on the ratification is a clean halt — got: {0}", result.Message);
                first.ChatClient.InvocationCount.Should().Be(2,
                    "one analyzer call + exactly one drafting call");
                runId = DurableDialogueTests.SingleRun(dbPath).Id;
            }

            var ratification = DurableDialogueTests.SingleCheckpoint(dbPath);
            ratification.QuestionJson.Should().Contain("Ratify");
            ratification.QuestionJson.Should().Contain("Empty payloads return 400.",
                "the question context carries the canonical draft block");

            // ---- Act 2: restart; the EDITED block answers the durable ask ----
            await using var second = DurableDialogueHarness.Build(Fixture, dbPath, jobQueue);
            await DurableDialogueTests.AnswerAsync(second, ratification, EditedAnswer);
            (await second.Services.GetRequiredService<DialogueResumeSweeper>()
                .ScanOnceAsync(CancellationToken.None)).Should().Be(1);
            await DurableDialogueHarness.BuildPump(second, Fixture, jobQueue)
                .TickAsync(CancellationToken.None);

            var resumed = await DurableDialogueHarness.ExecuteAsync(
                second, Fixture, jobQueue.DequeueViaJsonRoundTrip());
            resumed.IsSuccess.Should().BeTrue("the ratified run parks cleanly at the approval");
            second.ChatClient.ToolCalls.Should().BeEmpty(
                "the resumed negotiation must NOT re-draft — the draft is restored from the checkpoint");

            AssertRatifiedEdited(dbPath, runId);

            // ---- Act 3: approve the (second) park at Approval; run completes ----
            var approval = DurableDialogueTests.SingleCheckpoint(dbPath);
            approval.QuestionJson.Should().Contain("Approve");
            second.ChatClient
                .EnqueueToolCall("write_file", """{"path":"csharp-fixture/src/Patch.cs","content":"// fix"}""")
                .EnqueueText("""Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"patched"}""");
            await DurableDialogueTests.AnswerAsync(second, approval, "approve");
            (await second.Services.GetRequiredService<DialogueResumeSweeper>()
                .ScanOnceAsync(CancellationToken.None)).Should().Be(1);
            await DurableDialogueHarness.BuildPump(second, Fixture, jobQueue)
                .TickAsync(CancellationToken.None);

            var completed = await DurableDialogueHarness.ExecuteAsync(
                second, Fixture, jobQueue.DequeueViaJsonRoundTrip());

            completed.IsSuccess.Should().BeTrue("the fully ratified run must complete");
            var run = DurableDialogueTests.SingleRun(dbPath);
            run.Id.Should().Be(runId, "negotiate→edit→resume must never mint a second run row");
            run.Status.Should().Be("success");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    private static void AssertRatifiedEdited(string dbPath, string runId)
    {
        using var ctx = DurableDialogueTests.Db(dbPath);
        var expectation = ctx.RunExpectations.Single();
        expectation.RunId.Should().Be(runId);
        expectation.Outcome.Should().Be(ExpectationOutcomes.Edited);
        expectation.RatifiedBy.Should().Be("@operator");
        expectation.EditDistance.Should().BeGreaterThan(0);
        expectation.RatifiedJson.Should().Contain("Empty payloads return 422.",
            "the operator's edit is the ratified contract");
        expectation.DraftJson.Should().Contain("Empty payloads return 400.",
            "the original draft stays recorded for the p0329 metric");
    }
}
