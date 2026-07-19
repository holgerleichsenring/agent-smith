using AgentSmith.Application.Services;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

// p0341e: the run summary once showed $0.14 while the master truly spent $16.38 across 353
// LLM calls. Root cause: the shared PipelineCostTracker was fed ONLY the
// FunctionInvokingChatClient's final aggregate AFTER the loop (Track(loopResult.Response)),
// so a pass that ended by THROWING (the within-pass money fence / an LLM-layer timeout) dropped
// its ENTIRE spend. The fix feeds the tracker PER ITERATION via the governor's
// RecordIterationUsage hook. These tests pin that the tracker accrues EVERY iteration of a
// multi-iteration tool loop — not just the last call — and that the spend already landed when a
// later iteration throws.
public sealed class MasterLoopCostAccrualTests
{
    // A fake provider client that emits a tool call for the first (calls-1) iterations (forcing
    // the FunctionInvokingChatClient to loop) then a final text turn — each with its own usage.
    private sealed class MultiIterationInner : IChatClient
    {
        private readonly int _toolIterations;
        private int _call;

        public MultiIterationInner(int toolIterations) => _toolIterations = toolIterations;

        public int Calls => _call;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var index = _call++;
            var usage = new UsageDetails { InputTokenCount = 1000, OutputTokenCount = 500 };
            ChatMessage message = index < _toolIterations
                ? new ChatMessage(ChatRole.Assistant,
                    new AIContent[] { new FunctionCallContent($"c{index}", "noop", new Dictionary<string, object?>()) })
                : new ChatMessage(ChatRole.Assistant, "done");
            return Task.FromResult(new ChatResponse(message) { Usage = usage, ModelId = "test-model" });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static IChatClient BuildLoop(IChatClient inner, MasterLoopHooks hooks) =>
        new ChatClientBuilder(new MasterLoopGovernorChatClient(inner, hooks))
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = 25)
            .Build();

    [Fact]
    public async Task SharedTracker_FedPerIteration_AccruesEveryToolLoopIteration_NotJustTheFinalCall()
    {
        var tracker = new PipelineCostTracker();
        // Mirror BuildMasterLoopHooks: RecordIterationUsage feeds the shared tracker per iteration.
        var hooks = new MasterLoopHooks(
            RecordIterationUsage: tracker.Track,
            ReminderEveryNIterations: 0, DriftEditlessIterations: 0);
        var inner = new MultiIterationInner(toolIterations: 3); // 3 tool turns + 1 final = 4 provider calls
        var loop = BuildLoop(inner, hooks);
        var tools = new List<AITool> { AIFunctionFactory.Create(() => "ok", name: "noop") };

        await loop.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "go") },
            new ChatOptions { Tools = tools });

        inner.Calls.Should().Be(4, "the loop made 3 tool iterations plus a final answer");
        // The tracker must reflect ALL FOUR provider calls, not just the last one.
        tracker.CallCount.Should().Be(4);
        tracker.TotalInputTokens.Should().Be(4000);
        tracker.TotalOutputTokens.Should().Be(2000);
    }

    [Fact]
    public async Task SharedTracker_SpendAlreadyRecorded_WhenAPassThrowsMidLoop()
    {
        var tracker = new PipelineCostTracker();
        var iterations = 0;
        var hooks = new MasterLoopHooks(
            // Trip the fence after 3 recorded iterations — the analogue of the within-pass money
            // fence firing mid-pass; the governor throws MasterBudgetExhaustedException.
            IsBudgetExhausted: () => iterations >= 3,
            RecordIterationUsage: r => { iterations++; tracker.Track(r); },
            ReminderEveryNIterations: 0, DriftEditlessIterations: 0);
        var inner = new MultiIterationInner(toolIterations: 10);
        var loop = BuildLoop(inner, hooks);
        var tools = new List<AITool> { AIFunctionFactory.Create(() => "ok", name: "noop") };

        var act = async () => await loop.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "go") },
            new ChatOptions { Tools = tools });

        await act.Should().ThrowAsync<MasterBudgetExhaustedException>();
        // Even though the pass threw, the 3 iterations that ran were ALREADY recorded in the
        // shared tracker — the summary is no longer $0 for a run that spent real money.
        tracker.CallCount.Should().Be(3);
        tracker.TotalInputTokens.Should().Be(3000);
    }
}
