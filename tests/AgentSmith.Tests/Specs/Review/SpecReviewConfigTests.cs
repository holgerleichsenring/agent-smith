using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762g: the spec_review block's defaults and its one rule. 2026-11-19-4176: the re-ground read
/// budget is all that is left of it.</summary>
public sealed class SpecReviewConfigTests
{
    [Fact]
    public void SpecReviewConfig_Default_IsAReGroundBudgetOfTwentyFour()
    {
        var config = new AgentSmithConfig().SpecReview;

        config.RegroundReadBudget.Should().Be(24);
        config.Problems().Should().BeEmpty();
    }

    [Fact]
    public void SpecReviewConfig_ReGroundBudgetBelowOne_NamesItsKey() =>
        new SpecReviewConfig { RegroundReadBudget = 0 }.Problems().Should().Equal(
            "spec_review.reground_read_budget is 0; it must be 1 or more");
}
