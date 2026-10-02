using AgentSmith.Application.Services.Lifecycle;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5f89e: the suspend-gap guard extracted from ActiveRunReaper, now shared with
/// RunLivenessReaper. On wake every heartbeat is stale by construction; verdicts wait one
/// grace window.
/// </summary>
public sealed class SuspendGapDetectorTests
{
    private static readonly TimeSpan Scan = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(3);
    private readonly SteppedMonotonicClock _clock = new();

    [Fact]
    public void Observe_NormalCadence_FindsNoGapAndAllowsVerdicts()
    {
        var detector = new SuspendGapDetector(_clock, Scan, Grace);
        _clock.Advance(Scan);

        detector.Observe().Should().BeNull();
        detector.SuppressesVerdicts().Should().BeFalse();
    }

    [Fact]
    public void Observe_AGapBeyondTheCadence_SuppressesVerdictsForOneGraceWindow()
    {
        var detector = new SuspendGapDetector(_clock, Scan, Grace);
        _clock.Advance(TimeSpan.FromMinutes(30));

        detector.Observe().Should().Be(TimeSpan.FromMinutes(30));
        detector.SuppressesVerdicts().Should().BeTrue();

        for (var i = 0; i < 4; i++)
        {
            _clock.Advance(Scan);
            detector.Observe().Should().BeNull("a normal step after the wake is no second gap");
        }
        detector.SuppressesVerdicts().Should().BeFalse("the grace window has passed");
    }

    private sealed class SteppedMonotonicClock : TimeProvider
    {
        private long _timestamp;
        public override long GetTimestamp() => _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan by) => _timestamp += by.Ticks;
    }
}
