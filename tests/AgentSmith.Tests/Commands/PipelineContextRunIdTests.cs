using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-09-01-b467: the run id is the name the run's own commits carry on the branch, so
/// reading it is worth one accessor rather than a lookup repeated at every call site.
/// </summary>
public sealed class PipelineContextRunIdTests
{
    [Fact]
    public void RunId_ARunningPipeline_NamesItsRun()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-b467");

        pipeline.RunId().Should().Be("run-b467");
    }

    [Fact]
    public void RunId_OutsideARun_IsNull()
    {
        new PipelineContext().RunId().Should().BeNull();
    }

    [Fact]
    public void RunId_AnEmptyValue_IsNull()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "   ");

        pipeline.RunId().Should().BeNull("a blank id would search the history for every run's marker");
    }
}
