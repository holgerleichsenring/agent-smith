using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

public sealed class LimitEnforcerTests
{
    private static LimitEnforcer Build(LoopLimitsConfig limits, out CancellationTokenSource cts)
    {
        cts = new CancellationTokenSource();
        return new LimitEnforcer(limits, cts);
    }

    [Fact]
    public void RecordLlmCall_AccumulatesInputAndOutputTokens()
    {
        var enforcer = Build(new LoopLimitsConfig(), out _);

        enforcer.RecordLlmCall(100, 50);
        enforcer.RecordLlmCall(200, 75);

        enforcer.AccumulatedInputTokens.Should().Be(300);
        enforcer.AccumulatedOutputTokens.Should().Be(125);
    }

    [Fact]
    public void RecordLlmCall_LlmCallCountIncrements()
    {
        var enforcer = Build(new LoopLimitsConfig(), out _);

        enforcer.RecordLlmCall(10, 5);
        enforcer.RecordLlmCall(20, 10);
        enforcer.RecordLlmCall(30, 15);

        enforcer.LlmCallCount.Should().Be(3);
    }

    [Fact]
    public void RecordToolCall_ToolCallCountIncrements()
    {
        var enforcer = Build(new LoopLimitsConfig(), out _);

        enforcer.RecordToolCall("read_file", 5, true);
        enforcer.RecordToolCall("grep", 12, true);

        enforcer.ToolCallCount.Should().Be(2);
    }

    [Fact]
    public void CheckTimeLimit_BelowLimit_ReturnsTrue()
    {
        var enforcer = Build(new LoopLimitsConfig { MaxSecondsPerSkillCall = 60 }, out _);

        enforcer.CheckTimeLimit().Should().BeTrue();
    }

    [Fact]
    public void CheckTimeLimit_AboveLimit_SignalsCancellationTokenSource()
    {
        var enforcer = Build(new LoopLimitsConfig { MaxSecondsPerSkillCall = 0 }, out var cts);

        Thread.Sleep(50);
        var result = enforcer.CheckTimeLimit();

        result.Should().BeFalse();
        cts.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void RecordLlmCall_TokenLimitExceeded_ReturnsCapDecision()
    {
        var enforcer = Build(
            new LoopLimitsConfig { MaxInputTokensPerSkillCall = 100 },
            out var cts);

        var decision = enforcer.RecordLlmCall(150, 10);

        decision.IsContinue.Should().BeFalse();
        decision.Kind.Should().Be(LimitDecisionKind.CappedTokens);
        decision.Reason.Should().Contain("input tokens");
        cts.IsCancellationRequested.Should().BeTrue();
    }
}
