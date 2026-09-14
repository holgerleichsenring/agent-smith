using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Runs;

/// <summary>p0439: a delivered shortfall is published with its own status, never success or failed.</summary>
public sealed class RunTerminalStatusTests
{
    [Fact]
    public void Of_AFailedResult_IsFailed()
    {
        RunTerminalStatus.Of(CommandResult.Fail("boom"), new PipelineContext()).Should().Be(RunStatuses.Failed);
    }

    [Fact]
    public void Of_ASuccessfulResultWithNothingDelivered_IsSuccess()
    {
        RunTerminalStatus.Of(CommandResult.Ok("done"), new PipelineContext()).Should().Be(RunStatuses.Success);
    }

    [Fact]
    public void Of_ASuccessfulResultOfADeliveredShortfall_IsShortfall()
    {
        var pipeline = new PipelineContext();
        new RunShortfall(
            [new PhaseProgress("p0001a", "a", PhaseRunState.Done)],
            [new PhaseProgress("p0001b", "b", PhaseRunState.NotStarted)],
            "budget").MarkDelivered(pipeline);

        RunTerminalStatus.Of(CommandResult.Ok("delivered"), pipeline).Should().Be(RunStatuses.Shortfall);
    }

    [Theory]
    [InlineData("success", true)]
    [InlineData("shortfall", true)]
    [InlineData("failed", false)]
    [InlineData("cancelled", false)]
    [InlineData(null, false)]
    public void IsDelivered_SuccessAndShortfallOnly(string? status, bool expected)
    {
        RunStatuses.IsDelivered(status).Should().Be(expected);
    }
}
