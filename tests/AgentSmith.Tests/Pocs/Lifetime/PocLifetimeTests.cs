using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: a PoC's expiry and reminders, computed in memory from its facts and its target.</summary>
public sealed class PocLifetimeTests
{
    internal static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly PocTargetConfig Target = new() { Name = "main", LifetimeDays = 90, ReminderDays = [14, 7, 1] };
    private readonly PocLifetime _lifetime = new();

    private static PocLifetimeFacts Facts(long? expiresAt = null, DateTimeOffset? seen = null, int extensions = 0) =>
        new(Created, expiresAt, seen?.UtcTicks, extensions);

    [Fact]
    public void Of_NoStoredExpiry_ExpiresAtCreatedPlusLifetimeDays()
    {
        var state = _lifetime.Of(Facts(), Target, Created.AddDays(10));

        (state.ExpiresAt, state.IsExpired, state.IsWindowOpen, state.DaysLeft).Should().Be((Created.AddDays(90), false, false, 80));
    }

    [Fact]
    public void Of_FourteenDaysLeft_OpensTheWindow_WithTheFourteenDayReminderDue()
    {
        var state = _lifetime.Of(Facts(), Target, Created.AddDays(76));

        (state.IsWindowOpen, state.DaysLeft, state.DueReminderDays).Should().Be((true, 14, 14));
        _lifetime.Of(Facts(), Target, Created.AddDays(89.5)).DueReminderDays.Should().Be(1);
    }

    [Fact]
    public void Of_AfterTheExpiry_IsExpired_NoWindow_ZeroDaysLeft()
    {
        var state = _lifetime.Of(Facts(), Target, Created.AddDays(90));

        (state.IsExpired, state.IsWindowOpen, state.DaysLeft, state.DueReminderDays).Should().Be((true, false, 0, (int?)null));
    }

    [Fact]
    public void Of_ASeenTimeBeforeTheWindowOrAfterTheExpiry_DoesNotCount_OneInsideDoes()
    {
        var now = Created.AddDays(95);

        _lifetime.Of(Facts(seen: Created.AddDays(70)), Target, now).IsSeen.Should().BeFalse("the window opened on day 76");
        _lifetime.Of(Facts(seen: Created.AddDays(91)), Target, now).IsSeen.Should().BeFalse("a view after the expiry never counts");
        _lifetime.Of(Facts(seen: Created.AddDays(89)), Target, now).IsSeen.Should().BeTrue();
    }

    [Fact]
    public void Of_AfterAnExtension_TheOldSeenTimeDoesNotCount()
    {
        var extended = Created.AddDays(180).UtcTicks;

        var state = _lifetime.Of(Facts(extended, seen: Created.AddDays(89), extensions: 1), Target, Created.AddDays(181));

        (state.ExpiresAt, state.IsExpired, state.IsSeen).Should().Be((Created.AddDays(180), true, false));
    }

    [Fact]
    public void ExpiredOn_OnlyWhileExpired_AndNullWithoutFactsOrTarget()
    {
        _lifetime.ExpiredOn(Facts(), Target, Created.AddDays(91)).Should().Be(Created.AddDays(90));
        _lifetime.ExpiredOn(Facts(), Target, Created.AddDays(10)).Should().BeNull();
        _lifetime.ExpiredOn(null, Target, Created.AddDays(91)).Should().BeNull();
        _lifetime.ExpiredOn(Facts(), null, Created.AddDays(91)).Should().BeNull();
    }

    [Fact]
    public void DeletesAt_CountsFromAnExpiryStopAtOrAfterTheExpiry_NotFromAnEarlierOne()
    {
        var state = _lifetime.Of(Facts(), Target, Created.AddDays(100));

        _lifetime.DeletesAt(state, Created.AddDays(91).UtcTicks, 30).Should().Be(Created.AddDays(121));
        _lifetime.DeletesAt(state, Created.AddDays(50).UtcTicks, 30).Should().BeNull("an 'expired' entry of an earlier expiry");
        _lifetime.DeletesAt(state, null, 30).Should().BeNull();
    }

    [Fact]
    public void TargetOf_FindsTheTargetIgnoringCase_NullWhenGone()
    {
        var pocs = new PocsConfig { Targets = [Target] };

        _lifetime.TargetOf(pocs, "MAIN").Should().BeSameAs(Target);
        _lifetime.TargetOf(pocs, "other").Should().BeNull();
    }
}
