using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

public sealed class SkillCallRuntimeIntegrationTests
{
    [Fact]
    public async Task ExecuteAsync_FullLoop_ReturnsOk()
    {
        var chat = new ScriptedRuntimeChatClient(() => ScriptedRuntimeChatClient.Make("{\"answer\":42}"));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat);

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.Outcome.Should().Be(SkillCallOutcome.Ok);
        result.Output.Should().Be("{\"answer\":42}");
        result.Cost.Should().NotBeNull();
        result.Trace.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_PerSkillBreakdownCapturesCallCostRecord()
    {
        var chat = new ScriptedRuntimeChatClient(() => ScriptedRuntimeChatClient.Make("{}", input: 100, output: 50));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat);

        await runtime.ExecuteAsync(
            RuntimeBuilder.MakeRequest(SkillExecutionPhase.Plan),
            tracker, CancellationToken.None);

        tracker.PerSkillBreakdown.Should().HaveCount(1);
        tracker.PerSkillBreakdown[0].SkillName.Should().Be("test-skill");
        tracker.PerSkillBreakdown[0].Phase.Should().Be(SkillExecutionPhase.Plan);
        tracker.PerSkillBreakdown[0].InputTokens.Should().Be(100);
        tracker.PerSkillBreakdown[0].OutputTokens.Should().Be(50);
    }

    [Fact]
    public async Task ExecuteAsync_RuntimeReturnsTraceAndCostShape()
    {
        var chat = new ScriptedRuntimeChatClient(() => ScriptedRuntimeChatClient.Make("{}"));
        var (runtime, tracker, _) = RuntimeBuilder.Build(chat);

        var result = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        result.Cost.SkillName.Should().Be("test-skill");
        result.Cost.Role.Should().Be("planner");
        result.Trace.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_BudgetExhausted_ShortCircuitsWithCostCapObservation()
    {
        // Pre-spend the budget by running a first skill against a tiny cap.
        var chat = new ScriptedRuntimeChatClient(
            () => ScriptedRuntimeChatClient.Make("{}", input: 1000, output: 1000),
            () => ScriptedRuntimeChatClient.Make("{}"));
        var tracker = new PipelineCostTracker(
            config: null,
            costCap: new CostCapValues { Usd = 100m, Tokens = 100 });
        var factory = new StubRuntimeChatClientFactory(chat);
        var limits = new LoopLimitsConfig();
        var runtime = new Application.Services.Loop.SkillCallRuntime(
            factory, new Application.Services.Loop.PipelineConcurrencyGate(limits), limits,
            new Application.Services.Loop.OutcomeClassifier(),
            new Application.Services.Loop.RetryCoordinator(),
            new Application.Services.Validation.SkillOutputValidatorFactory(
                new Application.Services.Loop.NoOpSkillOutputValidator(),
                new Application.Services.Loop.NoOpSkillOutputValidator()),
            new Application.Services.Loop.RuntimeObservationFactory(),
            AgentSmith.Tests.TestHelpers.EventTestStubs.NoOp,
            AgentSmith.Tests.TestHelpers.EventTestStubs.RunContext,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Application.Services.Loop.SkillCallRuntime>.Instance);

        var first = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);
        first.Outcome.Should().Be(SkillCallOutcome.Ok);
        tracker.IsBudgetExhausted.Should().BeTrue();

        var second = await runtime.ExecuteAsync(RuntimeBuilder.MakeRequest(), tracker, CancellationToken.None);

        second.Outcome.Should().Be(SkillCallOutcome.Incomplete);
        second.FailureReason.Should().Be("cost cap exhausted");
        second.RuntimeObservations.Should().HaveCount(1);
        second.RuntimeObservations[0].Category.Should().Be(ExecutionLimitCategories.CostCapExhausted);
        chat.CallCount.Should().Be(1, "the second call must short-circuit before invoking the chat client");
    }
}
