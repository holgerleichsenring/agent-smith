using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0367: tool-call events are kept for metrics but their DB writes are batched,
/// not one insert per event on the broadcast hot path. The buffer holds events
/// until the flush threshold, then hands back the whole batch at once.
/// </summary>
public sealed class RunTrailBufferTests
{
    private const string RunId = "run-1";
    private const int Threshold = 25;

    [Fact]
    public void Add_ToolCallEventsBelowThreshold_BuffersWithoutFlush()
    {
        var buffer = new RunTrailBuffer();

        for (var i = 0; i < Threshold - 1; i++)
            buffer.Add(Command(), Threshold).Should().BeNull("writes accumulate, not one-per-event");
    }

    [Fact]
    public void Add_AtThreshold_FlushesTheWholeBatchOnce()
    {
        var buffer = new RunTrailBuffer();
        for (var i = 0; i < Threshold - 1; i++) buffer.Add(Command(), Threshold);

        var batch = buffer.Add(Command(), Threshold);

        batch.Should().NotBeNull();
        batch!.Should().HaveCount(Threshold);
        buffer.Add(Command(), Threshold).Should().BeNull("the buffer reset after flushing");
    }

    private static SandboxCommandEvent Command() =>
        new(RunId, "default", "dotnet", 4, DateTimeOffset.UtcNow);
}
