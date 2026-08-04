using FluentAssertions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>p0397a: the eval money fence — fast-tier tests, no LLM.</summary>
public sealed class PlanCallEvalBudgetTests
{
    [Fact]
    public void WorstCase_KnownModel_InputEstimatePlusFullCap()
    {
        var budget = new PlanCallEvalBudget();
        // gpt-4.1: $2/M in, $8/M out; 40_000 chars ~ 10_000 tokens.
        var worst = budget.WorstCaseUsd("gpt-4.1", promptChars: 40_000, maxOutputTokens: 8192);
        worst.Should().BeApproximately(10_000m * 2 / 1_000_000 + 8192m * 8 / 1_000_000, 0.0001m);
    }

    [Fact]
    public void WorstCase_UnknownModel_PricesAtTheCeiling()
    {
        var budget = new PlanCallEvalBudget();
        var unknown = budget.WorstCaseUsd("mystery-model", 4_000, 1_000);
        var opus = budget.WorstCaseUsd("claude-opus-x", 4_000, 1_000);
        unknown.Should().Be(opus);
    }

    [Fact]
    public void Allows_RefusesWhenWorstCaseExceedsRemaining()
    {
        var budget = new PlanCallEvalBudget();
        // Spend nearly the whole default budget, then a big call must be refused.
        budget.RecordActual("claude-opus-x", 0, inputTokens: 300_000, outputTokens: 4_000);
        budget.Allows("claude-sonnet-5", 40_000, 16_384).Should().BeFalse();
        budget.Allows("gpt-4.1", 4_000, 256).Should().BeTrue();
    }

    [Fact]
    public void RecordActual_AccumulatesFromReturnedUsage()
    {
        var budget = new PlanCallEvalBudget();
        var first = budget.RecordActual("gpt-4.1", 0, 10_000, 2_000);
        var second = budget.RecordActual("gpt-4.1", 0, 10_000, 2_000);
        first.Should().Be(second);
        budget.SpentUsd.Should().Be(first + second);
    }

    [Fact]
    public void RecordActual_NoReportedInput_FallsBackToCharEstimate()
    {
        var budget = new PlanCallEvalBudget();
        var cost = budget.RecordActual("gpt-4.1", promptChars: 40_000, inputTokens: null, outputTokens: 0);
        cost.Should().BeApproximately(10_000m * 2 / 1_000_000, 0.0001m);
    }
}
