using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

public sealed class SkillCallRuntimeLimitTests
{
    [Fact]
    public async Task ExecuteAsync_ConcurrencyLimitReached_BlocksUntilSlotAvailable()
    {
        var slowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var chat = ScriptedRuntimeChatClient.Async(
            async () =>
            {
                await slowGate.Task;
                return ScriptedRuntimeChatClient.Make("{}");
            },
            () => Task.FromResult(ScriptedRuntimeChatClient.Make("{}")));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat,
            new LoopLimitsConfig { MaxConcurrentSkillCalls = 1 });

        var first = runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);
        var second = runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        await Task.Delay(50);
        second.IsCompleted.Should().BeFalse();
        slowGate.SetResult();

        await first.WaitAsync(TimeSpan.FromSeconds(2));
        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(2));
        secondResult.Outcome.Should().Be(SkillCallOutcome.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_VerifyDiffMode_UsesVerifierToolCallCap()
    {
        var chat = new ScriptedRuntimeChatClient(() => ScriptedRuntimeChatClient.Make("{}"));
        var limits = new LoopLimitsConfig { MaxToolCallsPerVerifier = 42 };
        var (runtime, tracker, factory) = RuntimeBuilder.Build(chat, limits);

        await runtime.ExecuteAsync(
            RuntimeBuilder.MakeRequest(SkillExecutionPhase.Verify, "verify_diff"),
            tracker, CancellationToken.None);

        factory.LastMaxIterations.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_VerifyHintMode_UsesInvestigatorToolCallCap()
    {
        var chat = new ScriptedRuntimeChatClient(() => ScriptedRuntimeChatClient.Make("{}"));
        var limits = new LoopLimitsConfig { MaxToolCallsPerInvestigator = 7 };
        var (runtime, tracker, factory) = RuntimeBuilder.Build(chat, limits);

        await runtime.ExecuteAsync(
            RuntimeBuilder.MakeRequest(SkillExecutionPhase.Investigate, "verify_hint"),
            tracker, CancellationToken.None);

        factory.LastMaxIterations.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_PlanPhase_UsesSkillToolCallCap()
    {
        var chat = new ScriptedRuntimeChatClient(() => ScriptedRuntimeChatClient.Make("{}"));
        var limits = new LoopLimitsConfig { MaxToolCallsPerSkill = 99 };
        var (runtime, tracker, factory) = RuntimeBuilder.Build(chat, limits);

        await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        factory.LastMaxIterations.Should().Be(99);
    }

    [Fact]
    public async Task ExecuteAsync_TimeLimitReached_ReturnsIncomplete()
    {
        var chat = new ScriptedRuntimeChatClient(() =>
        {
            Thread.Sleep(50);
            return ScriptedRuntimeChatClient.Make("{}");
        });
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat,
            new LoopLimitsConfig { MaxSecondsPerSkillCall = 0 });

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.Outcome.Should().Be(SkillCallOutcome.Incomplete);
    }
}
