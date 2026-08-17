using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0423: statistics are a QUERY over the recorded units. A counter kept beside the events
/// is a second answer to the same question, and two answers drift.
/// </summary>
public sealed class RunCallStatisticsTests
{
    /// <summary>
    /// The shape that named the wall in run 26: the prompt grows, the answer shrinks, and
    /// the last call ends without producing anything at all.
    /// </summary>
    [Fact]
    public void PhaseStatistics_AreDerivedFromUnits_NotFromCounters()
    {
        var events = new RunEvent[]
        {
            Call(promptChars: 151_040, responseChars: 3_886, durationMs: 9_300),
            Call(promptChars: 216_004, responseChars: 3_886, durationMs: 94_000),
            Call(promptChars: 278_267, responseChars: 2_750, durationMs: 92_700),
            Call(promptChars: 340_747, responseChars: 969, durationMs: 121_300),
            Call(promptChars: 356_632, responseChars: 0, durationMs: 1_407_000, WorkOutcome.Cancelled),
        };

        var stats = RunCallStatistics.From(events);

        stats.Calls.Should().Be(5);
        stats.FailedCalls.Should().Be(1, "a call that never produced an answer still ended");
        stats.LargestPromptChars.Should().Be(356_632);
        stats.SmallestResponseChars.Should().Be(0);
        stats.TotalDurationMs.Should().Be(1_724_300);
    }

    [Fact]
    public void WhatTheBoundCut_IsCountedAcrossTheRun()
    {
        var events = new RunEvent[]
        {
            new ToolResultEvent("run", "read_file", true, 100_048, DateTimeOffset.UtcNow,
                UnboundedResultLength: 4_135_613),
            new ToolResultEvent("run", "read_file", true, 512, DateTimeOffset.UtcNow),
        };

        var stats = RunCallStatistics.From(events);

        stats.ToolCalls.Should().Be(2);
        stats.ToolOutputChars.Should().Be(4_136_125);
        stats.ToolCharsNeverDelivered.Should().Be(4_035_565);
    }

    [Fact]
    public void ARunWithNoCalls_AnswersZero_NotAnException()
    {
        var stats = RunCallStatistics.From([]);

        stats.Calls.Should().Be(0);
        stats.LargestPromptChars.Should().Be(0);
        stats.SmallestResponseChars.Should().Be(0);
    }

    private static LlmCallFinishedEvent Call(
        long promptChars, long responseChars, long durationMs, WorkOutcome outcome = WorkOutcome.Ok) =>
        new("run", "sonnet", "agentic-executor", 0, 0, 0m, durationMs, DateTimeOffset.UtcNow,
            PromptChars: promptChars, ResponseChars: responseChars, Outcome: outcome);
}
