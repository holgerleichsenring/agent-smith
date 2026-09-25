using AgentSmith.Contracts.Constants;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;
using FluentAssertions;
using Xunit.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-23-4722a: the one claim in this phase that the assembly cannot settle.
///
/// Verified offline, and pinned by the unit tests: a declaration with no AIFunction behind it is
/// left PENDING by the runtime and surfaces as an external-tool request. NOT verifiable offline,
/// because it lives in the CLI runtime rather than in the SDK: that the pending call SURVIVES
/// GetResponseAsync returning, that no deadline expires while our own loop runs the tool, and that
/// answering it on a LATER call resumes the turn. The whole parity design rests on those three.
///
/// This test is the proof, and it needs a real seat and a real runtime. It SKIPS loudly when either
/// is absent rather than passing quietly, because a green tick that proved nothing is worse than a
/// gap that says so.
/// </summary>
[Trait("Category", "LiveLLM")]
public sealed class CopilotPendingCallLivenessTests(ITestOutputHelper output)
{
    private static string? Seat => RequiresCopilotSeatFactAttribute.Seat;

    /// <summary>How long our loop is pretended to take before the answer goes back. Settable so the
    /// tolerance can be measured: 2026-09-25-6b2e proved the failure was ours by showing it was the
    /// same at 2 seconds as at 90, and what the runtime really tolerates is still unmeasured.</summary>
    private static int DelaySeconds =>
        int.TryParse(Environment.GetEnvironmentVariable("COPILOT_LIVENESS_DELAY_SECONDS"), out var d)
            ? d : 90;

    [RequiresCopilotSeatFact]
    public async Task PendingToolCall_SurvivesTheCallReturning_AndResumesWhenAnswered()
    {
        var runtime = new CopilotRuntime(NullLoggerFactory.Instance);
        var template = new CopilotSessionRequest(
            Model: null, ReasoningEffort: null,
            SystemMessage: "Call the tool you are given. Do not answer without calling it.",
            SeatToken: Seat, Tools: []);
        var client = new CopilotSessionChatClient(
            runtime, template, NullLogger<CopilotSessionChatClient>.Instance);

        var options = new ChatOptions
        {
            Tools = [AIFunctionFactory.Create(
                string () => throw new InvalidOperationException("the runtime must not run this"),
                "read_the_number",
                "Returns the number the user asked for.")],
        };

        var first = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Use read_the_number and tell me what it returns.")], options);

        var call = first.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Single();
        first.FinishReason.Should().Be(ChatFinishReason.ToolCalls);

        // The point of the test: our loop takes its time, and the pending call must still be there.
        output.WriteLine($"answering tool call {call.CallId} after {DelaySeconds}s");
        await Task.Delay(TimeSpan.FromSeconds(DelaySeconds));

        var second = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, "41")])], options);

        second.Text.Should().Contain("41", "the session resumed from the answer we gave it");
        second.FinishReason.Should().Be(ChatFinishReason.Stop);
    }
}
