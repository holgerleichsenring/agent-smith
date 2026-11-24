using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: pocs.lifetime's values are refused, never clamped, each naming its value, reason and key.</summary>
public sealed class PocLifetimeConfigTests
{
    [Fact]
    public void Problems_Defaults_None()
    {
        new PocLifetimeConfig().Problems().Should().BeEmpty();
        (new PocLifetimeConfig().IntervalSeconds, new PocLifetimeConfig().ExtendDays, new PocLifetimeConfig().MaxOwnerExtensions,
            new PocLifetimeConfig().UnseenDeleteAfterDays).Should().Be((3600, 90, 2, 30));
    }

    [Fact]
    public void Problems_AnIntervalOf60_IsRefusedNamingValueReasonAndKey()
    {
        new PocLifetimeConfig { IntervalSeconds = 60 }.Problems().Should().ContainSingle()
            .Which.Should().Contain("pocs.lifetime.interval_seconds is 60").And.Contain("reminders count in days");
    }

    [Fact]
    public void Problems_NegativeOwnerExtensions_AndZeroExtendDays_AreRefused()
    {
        new PocLifetimeConfig { MaxOwnerExtensions = -1, ExtendDays = 0 }.Problems().Should().HaveCount(2)
            .And.Contain(p => p.Contains("pocs.lifetime.max_owner_extensions is -1")).And.Contain(p => p.Contains("pocs.lifetime.extend_days is 0"));
    }

    [Fact]
    public void Problems_UnseenDeleteAfterZeroDays_IsRefusedNamingValueReasonAndKey()
    {
        new PocLifetimeConfig { UnseenDeleteAfterDays = 0 }.Problems().Should().ContainSingle()
            .Which.Should().Contain("pocs.lifetime.unseen_delete_after_days is 0").And.Contain("before its owner could see the banner");
    }

    [Fact]
    public void PocsConfigProblems_IncludeTheLifetimeBlock()
    {
        new PocsConfig { Lifetime = new PocLifetimeConfig { UnseenDeleteAfterDays = 0 } }.Problems()
            .Should().Contain(p => p.Contains(PocLifetimeConfig.UnseenDeleteAfterDaysKey));
    }
}
