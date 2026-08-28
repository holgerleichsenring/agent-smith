using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

public sealed class SkillCallRuntimeLimitTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task ExecuteAsync_ConcurrencyLimitReached_BlocksUntilSlotAvailable()
    {
        // 2026-08-28-479f: the first call announces that it is INSIDE the gate, and the
        // test waits for that instead of sleeping. The claim is about the semaphore —
        // with the only slot held, the second call cannot have completed — and a sleep
        // was standing in for a signal the call can give itself. Under three parallel
        // test processes the 50 ms sleep took tens of seconds and the two-second waits
        // behind it expired, failing four consecutive gate runs of unrelated phases.
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var chat = ScriptedRuntimeChatClient.Async(
            async () =>
            {
                firstEntered.TrySetResult();
                await slowGate.Task;
                return ScriptedRuntimeChatClient.Make("{}");
            },
            () => Task.FromResult(ScriptedRuntimeChatClient.Make("{}")));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat,
            new LoopLimitsConfig { MaxConcurrentSkillCalls = 1 });

        var first = runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);
        var second = runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        // A ceiling that still fails a hang, high enough that a starved thread pool does not.
        await firstEntered.Task.WaitAsync(Budget);
        second.IsCompleted.Should().BeFalse(
            "the only slot is held by the first call, which is inside the gate");
        slowGate.SetResult();

        await first.WaitAsync(Budget);
        var secondResult = await second.WaitAsync(Budget);
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

    // p0147b: Incomplete + FailedRuntime outcomes carry a typed
    // SkillObservation in RuntimeObservations so the round handler can
    // surface the silent skill drop in the final pipeline summary.

    [Fact]
    public async Task ExecuteAsync_TimeLimitReached_EmitsWallClockObservation()
    {
        var chat = new ScriptedRuntimeChatClient(() =>
        {
            Thread.Sleep(50);
            return ScriptedRuntimeChatClient.Make("{}");
        });
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat,
            new LoopLimitsConfig { MaxSecondsPerSkillCall = 0 });

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.RuntimeObservations.Should().ContainSingle();
        var obs = result.RuntimeObservations[0];
        obs.Category.Should().Be(ExecutionLimitCategories.ExecutionLimitWallClock);
        obs.Severity.Should().Be(ObservationSeverity.Info);
        obs.Blocking.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_OkOutcome_EmitsNoRuntimeObservation()
    {
        var chat = new ScriptedRuntimeChatClient(() => ScriptedRuntimeChatClient.Make("{}"));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat);

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.Outcome.Should().Be(SkillCallOutcome.Ok);
        result.RuntimeObservations.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_UncaughtException_EmitsExecutionErrorObservation()
    {
        var chat = new ScriptedRuntimeChatClient(() =>
            throw new InvalidOperationException("boom"));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat);

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.Outcome.Should().Be(SkillCallOutcome.FailedRuntime);
        result.RuntimeObservations.Should().ContainSingle();
        result.RuntimeObservations[0].Category.Should().Be(ExecutionLimitCategories.ExecutionError);
        result.RuntimeObservations[0].Description.Should().Contain("boom");
    }
}
