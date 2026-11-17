using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762g: the spec_review block's defaults and its one rule.</summary>
public sealed class SpecReviewConfigTests
{
    [Fact]
    public void SpecReviewConfig_Problems_RejectsShareOutsideOneToHundredAndReviewersBelowOne()
    {
        new SpecReviewConfig { BudgetSharePercent = 0 }.Problems().Should().ContainSingle()
            .Which.Should().Contain("budget_share_percent is 0");
        new SpecReviewConfig { BudgetSharePercent = 101 }.Problems().Should().ContainSingle();
        new SpecReviewConfig { Reviewers = 0, MaxConcurrent = 0, Rounds = -1 }.Problems().Should().HaveCount(3);
        new SpecReviewConfig { Rounds = 0, BudgetSharePercent = 100 }.Problems().Should().BeEmpty("0 rounds turns review off");
    }

    [Fact]
    public void SpecReviewConfig_Defaults_AreFourRoundsTwoReviewersTwoConcurrentFortyPercent()
    {
        var config = new AgentSmithConfig().SpecReview;

        (config.Rounds, config.Reviewers, config.MaxConcurrent, config.BudgetSharePercent).Should().Be((4, 2, 2, 40));
        config.Problems().Should().BeEmpty();
    }

    [Fact]
    public void SpecReviewConfig_DesignTurn_DefaultsOn() =>
        new AgentSmithConfig().SpecReview.DesignTurn.Should().BeTrue("2026-11-12-7c1c: a design turn's advisory pass runs unless turned off");

    [Fact]
    public void SpecReviewConfig_RatifiedBounds_DefaultToTheBuiltValuesAndNameTheirKeyBelowOne()
    {
        var config = new SpecReviewConfig();
        (config.AttackLookFloor, config.RegroundReadBudget, config.SeriesContractMaxChars).Should().Be((24, 24, 60_000));

        new SpecReviewConfig { AttackLookFloor = 0, RegroundReadBudget = 0, SeriesContractMaxChars = 0 }.Problems().Should().BeEquivalentTo(
            "spec_review.attack_look_floor is 0; it must be 1 or more",
            "spec_review.reground_read_budget is 0; it must be 1 or more",
            "spec_review.series_contract_max_chars is 0; it must be 1 or more");
    }
}
