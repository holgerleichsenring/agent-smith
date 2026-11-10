using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>2026-11-10-8e59: the run's result line names a review stop, so an attempt that built nothing says why.</summary>
public sealed class PipelineCompletedLineTests
{
    [Fact]
    public void For_ReviewStoppedTheAttempt_NamesTheSpecAndTheReason()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.SpecSequenceStop,
            new SpecSequenceStop("2026-10-14-5678a", "no reviewer answered on 2026-10-14-5678a's text in round 4", Park: false));

        PipelineCompletedLine.For(context).Should()
            .Contain("stopped before 2026-10-14-5678a").And.Contain("no reviewer answered");
    }

    [Fact]
    public void For_NoStop_IsThePlainCompletion() =>
        PipelineCompletedLine.For(new PipelineContext()).Should().Be(PipelineCompletedLine.Completed);
}
