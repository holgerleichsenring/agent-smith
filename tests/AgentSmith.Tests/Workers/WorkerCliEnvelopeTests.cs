using System.Globalization;
using System.Text.Json;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Workers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// 2026-09-01-b0d7: a worker call answered through the CLI's structured result. The
/// envelope must be unwrapped at the transport, so everything the bridge already does —
/// nudging on silence, executing tool calls, failing loudly — keeps deciding on the ANSWER
/// and never on a wrapper that is never empty.
/// </summary>
public sealed class WorkerCliEnvelopeTests
{
    private static string Envelope(
        string result, bool isError = false, string subtype = "success",
        long cacheCreate = 15162, decimal costUsd = 0.0215711m) =>
        // Formatted invariantly: a comma decimal separator is not JSON.
        Json(result, isError, subtype, cacheCreate,
            costUsd.ToString(CultureInfo.InvariantCulture));

    private static string Json(
        string result, bool isError, string subtype, long cacheCreate, string costUsd) =>
        $$$"""
          {"type":"result","subtype":"{{{subtype}}}","is_error":{{{(isError ? "true" : "false")}}},
           "duration_ms":2368,"num_turns":3,
           "result":{{{JsonSerializer.Serialize(result)}}},
           "session_id":"19b77a13","total_cost_usd":{{{costUsd}}},
           "usage":{"input_tokens":9,"cache_creation_input_tokens":{{{cacheCreate}}},
                    "cache_read_input_tokens":20446,"output_tokens":113,"service_tier":"standard"},
           "modelUsage":{"claude-haiku-4-5-20251001":{"inputTokens":9,"outputTokens":113,
                         "costUSD":{{{costUsd}}},"contextWindow":200000}},
           "permission_denials":[],"terminal_reason":"completed","uuid":"ea465652"}
          """;

    private static (ExternalWorkerChatClient Client, IRunContextAccessor Context)
        NewClient(IWorkerProcessRunner runner)
    {
        var json = new WorkerJsonFormat();
        var context = new AsyncLocalRunContextAccessor();
        return (new ExternalWorkerChatClient(
            new WorkerRequestComposer(new WorkerMessageMapper(json), new WorkerOptionsMapper()),
            new WorkerPromptRenderer(json), new WorkerReplyParser(json), new WorkerReplyTranslator(),
            runner, context,
            new ExternalWorkerCliOptions("claude", ["-p"], TimeSpan.FromMinutes(5), "/tmp"),
            "external_worker", "sonnet", NullLogger.Instance), context);
    }

    private static async Task<ChatResponse> Answer(
        IWorkerProcessRunner runner, ChatOptions? options = null)
    {
        var (client, context) = NewClient(runner);
        using var run = context.BeginScope("run-42");
        using var step = context.BeginStepScope(7);
        using var call = context.BeginCallScope("coding-master", "Implementation", "primary");
        return await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")], options, CancellationToken.None);
    }

    [Fact]
    public async Task WorkerCall_WithAnEnvelope_ReportsItsTokenUsage()
    {
        var response = await Answer(new ScriptedWorkerProcessRunner()
            .EnqueueRaw(Envelope("Adding the guard.")));

        response.Text.Should().Be("Adding the guard.",
            "the answer is what the model said, not the wrapper it arrived in");
        response.Usage!.InputTokenCount.Should().Be(9);
        response.Usage.OutputTokenCount.Should().Be(113);
        response.Usage.AdditionalCounts!["cache_read_input_tokens"].Should().Be(20446);
        response.Usage.AdditionalCounts["cache_creation_input_tokens"].Should().Be(15162);
        response.Usage.CachedInputTokenCount.Should().BeNull(
            "the CLI follows Anthropic semantics — input_tokens already excludes cache reads, "
            + "and the OpenAI cached-input path would both double-count and subtract them");
        response.ModelId.Should().Be("claude-haiku-4-5-20251001",
            "the model comes from what the CLI named, not from the 'sonnet' alias");
    }

    [Fact]
    public async Task WorkerCall_WithAnEnvelope_CarriesTheCliCostFigureInItsOwnChannel()
    {
        var tracker = new PipelineCostTracker();

        tracker.Track(await Answer(new ScriptedWorkerProcessRunner().EnqueueRaw(Envelope("ok"))));

        tracker.WorkerCalls.CallCount.Should().Be(1);
        tracker.WorkerCalls.ReportedCostUsd.Should().Be(0.0215711m);
        tracker.WorkerCalls.Models.Should().Be("claude-haiku-4-5-20251001");
        tracker.WorkerCalls.CacheCreationTokens.Should().Be(15162);
        tracker.BuildSummary()!.WorkerSpend!.ReportedCostUsd.Should().Be(0.0215711m);
    }

    /// <summary>
    /// The failure this phase is bigger than it looks for. With a structured result stdout
    /// is never empty, so an empty ANSWER would sail past the empty-turn check and be
    /// thrown as an unparseable reply — the exact shape that once threw away eleven minutes
    /// of verified work.
    /// </summary>
    [Fact]
    public async Task WorkerCall_EnvelopeWithEmptyResult_IsAnEmptyTurnNotAFailure()
    {
        var response = await Answer(new ScriptedWorkerProcessRunner().EnqueueRaw(Envelope("")));

        response.Text.Should().BeEmpty();
        response.FinishReason.Should().Be(ChatFinishReason.Stop);
    }

    [Fact]
    public async Task WorkerCall_EnvelopeWithAReportedError_FailsWithItsReason()
    {
        var act = () => Answer(new ScriptedWorkerProcessRunner()
            .EnqueueRaw(Envelope("", isError: true, subtype: "error_max_turns")));

        (await act.Should().ThrowAsync<ExternalWorkerCallException>(
                "a CLI that reports its own error EXITS ZERO, so the exit code alone would "
                + "call it a good call"))
            .Which.Reason.Should().Contain("error_max_turns").And.Contain("completed");
    }

    [Fact]
    public async Task WorkerCall_PlainTextOutput_StillAnswersTheCall()
    {
        var response = await Answer(new ScriptedWorkerProcessRunner()
            .EnqueueRaw("The guard already exists in Guard.cs."));

        response.Text.Should().Be("The guard already exists in Guard.cs.");
        response.Usage.Should().BeNull("a worker without the flag reports nothing to account");
    }

    [Fact]
    public async Task WorkerCall_ToolCallReply_IsUnaffectedByTheUnwrap()
    {
        var tool = AIFunctionFactory.Create((string path) => "written", "write_file", "Writes");

        var response = await Answer(
            new ScriptedWorkerProcessRunner()
                .EnqueueRaw(Envelope("""{"tool_calls":[{"name":"write_file","arguments":{"path":"src/A.cs"}}]}""")),
            new ChatOptions { Tools = [tool] });

        response.Messages[0].Contents.OfType<FunctionCallContent>().Should().ContainSingle()
            .Which.Name.Should().Be("write_file");
        response.FinishReason.Should().Be(ChatFinishReason.ToolCalls);
    }

    /// <summary>
    /// Why the brace scanner must not be the discriminator: model prose routinely carries
    /// unbalanced braces, and a scanner would cut the span somewhere inside the answer.
    /// </summary>
    [Fact]
    public async Task WorkerCall_AnswerTextContainingBraces_IsNotMisCut()
    {
        const string Prose = "Use `if (x) { return; ` — the closing brace is missing } and }}} too.";

        var response = await Answer(new ScriptedWorkerProcessRunner().EnqueueRaw(Envelope(Prose)));

        response.Text.Should().Be(Prose);
    }
}
