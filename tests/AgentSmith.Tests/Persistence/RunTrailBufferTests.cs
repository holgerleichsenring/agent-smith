using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0367: tool-call events are kept for metrics but their DB writes are batched,
/// not one insert per event on the broadcast hot path. The buffer holds events
/// until the flush threshold, then hands back the whole batch at once.
/// p0376: a partial buffer also drains once its oldest pending event ages out, so
/// a sparse or paused run's trail surfaces without waiting for the size threshold.
/// </summary>
public sealed class RunTrailBufferTests
{
    private const string RunId = "run-1";
    private const int Threshold = 25;
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-07-24T12:00:00Z");

    [Fact]
    public void Add_ToolCallEventsBelowThreshold_BuffersWithoutFlush()
    {
        var buffer = new RunTrailBuffer();

        for (var i = 0; i < Threshold - 1; i++)
            buffer.Add(Command(), Threshold, T0).Should().BeNull("writes accumulate, not one-per-event");
    }

    [Fact]
    public void Add_AtThreshold_FlushesTheWholeBatchOnce()
    {
        var buffer = new RunTrailBuffer();
        for (var i = 0; i < Threshold - 1; i++) buffer.Add(Command(), Threshold, T0);

        var batch = buffer.Add(Command(), Threshold, T0);

        batch.Should().NotBeNull();
        batch!.Should().HaveCount(Threshold);
        buffer.Add(Command(), Threshold, T0).Should().BeNull("the buffer reset after flushing");
    }

    [Fact]
    public void DrainIfOlderThan_OldestPendingAgedOut_DrainsPartialBatch()
    {
        var buffer = new RunTrailBuffer();
        buffer.Add(Command(), Threshold, T0);
        buffer.Add(Command(), Threshold, T0.AddMilliseconds(100));

        var stale = buffer.DrainIfOlderThan(TimeSpan.FromMilliseconds(750), T0.AddSeconds(1));

        stale.Should().NotBeNull();
        stale!.Should().HaveCount(2, "the partial buffer drains once its oldest event is stale");
        buffer.DrainIfOlderThan(TimeSpan.FromMilliseconds(750), T0.AddSeconds(2))
            .Should().BeNull("nothing pending after the drain");
    }

    [Fact]
    public void DrainIfOlderThan_StillFresh_DoesNotDrain()
    {
        var buffer = new RunTrailBuffer();
        buffer.Add(Command(), Threshold, T0);

        buffer.DrainIfOlderThan(TimeSpan.FromMilliseconds(750), T0.AddMilliseconds(100))
            .Should().BeNull("the oldest pending event is younger than maxAge");
    }

    [Fact]
    public void DrainIfOlderThan_Empty_ReturnsNull()
    {
        var buffer = new RunTrailBuffer();

        buffer.DrainIfOlderThan(TimeSpan.FromMilliseconds(750), T0.AddSeconds(10))
            .Should().BeNull();
    }

    [Fact]
    public void Add_AfterDrain_ResetsAgeFromNextEvent()
    {
        var buffer = new RunTrailBuffer();
        buffer.Add(Command(), Threshold, T0);
        buffer.DrainIfOlderThan(TimeSpan.FromMilliseconds(750), T0.AddSeconds(1));

        // a new event starts a fresh age window from its own timestamp
        buffer.Add(Command(), Threshold, T0.AddSeconds(1));
        buffer.DrainIfOlderThan(TimeSpan.FromMilliseconds(750), T0.AddSeconds(1).AddMilliseconds(100))
            .Should().BeNull("the age window resets to the first event after a drain");
    }

    private static SandboxCommandEvent Command() =>
        new(RunId, "default", "dotnet", 4, DateTimeOffset.UtcNow);
}
