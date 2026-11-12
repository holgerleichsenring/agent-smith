using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

public sealed class LimitEnforcerHitLimitTests
{
    [Fact]
    public void HitLimit_NeverFires_StaysNull()
    {
        var enforcer = new LimitEnforcer(new LoopLimitsConfig(), new CancellationTokenSource());

        enforcer.RecordLlmCall(inputTokens: 100, outputTokens: 100);

        enforcer.HitLimit.Should().BeNull();
    }

    [Fact]
    public void HitLimit_InputTokenCapReached_RecordsTokens()
    {
        var limits = new LoopLimitsConfig { MaxInputTokensPerSkillCall = 50 };
        var enforcer = new LimitEnforcer(limits, new CancellationTokenSource());

        var decision = enforcer.RecordLlmCall(inputTokens: 100, outputTokens: 5);

        decision.Kind.Should().Be(LimitDecisionKind.CappedTokens);
        enforcer.HitLimit.Should().Be("tokens");
    }

    // 2026-11-12-cd4cl: output is counted, never capped — the input and cost caps are the budget.
    [Fact]
    public void RecordLlmCall_ManyOutputTokens_Continues()
    {
        var enforcer = new LimitEnforcer(new LoopLimitsConfig(), new CancellationTokenSource());

        var decision = enforcer.RecordLlmCall(inputTokens: 5, outputTokens: 10_000_000);

        decision.Kind.Should().Be(LimitDecisionKind.Continue);
        enforcer.AccumulatedOutputTokens.Should().Be(10_000_000);
        enforcer.HitLimit.Should().BeNull();
    }

    [Fact]
    public void HitLimit_FirstCapWins_NotOverwrittenByLaterFires()
    {
        var limits = new LoopLimitsConfig { MaxInputTokensPerSkillCall = 50 };
        var enforcer = new LimitEnforcer(limits, new CancellationTokenSource());

        enforcer.RecordLlmCall(inputTokens: 100, outputTokens: 5);
        enforcer.RecordLlmCall(inputTokens: 100, outputTokens: 5);

        enforcer.HitLimit.Should().Be("tokens");
    }
}
