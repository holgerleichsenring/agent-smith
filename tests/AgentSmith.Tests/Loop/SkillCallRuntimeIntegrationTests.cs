using AgentSmith.Application.Models;
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
}
