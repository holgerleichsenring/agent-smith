using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class PlanContextRendererTests
{
    [Fact]
    public void Merge_NoUpstream_ReturnsProjectContextOnly()
    {
        var pipeline = new PipelineContext();

        var merged = PlanContextRenderer.Merge("base context", pipeline);

        merged.Should().Be("base context");
    }

    [Fact]
    public void Merge_ConvergenceResult_AppendsStructuredSection()
    {
        var pipeline = new PipelineContext();
        var observations = new List<SkillObservation>
        {
            new(1, "scope-verifier", ObservationConcern.Correctness, "out of scope",
                "rewrite", Blocking: true, ObservationSeverity.High, Confidence: 80),
        };
        pipeline.Set(ContextKeys.ConvergenceResult,
            new ConvergenceResult(
                Consensus: false,
                Observations: observations,
                Links: [],
                AdditionalRoles: [],
                Blocking: observations,
                NonBlocking: []));

        var merged = PlanContextRenderer.Merge("base", pipeline);

        merged.Should().Contain("base");
        merged.Should().Contain("Multi-Role Analysis (Structured)");
        merged.Should().Contain("out of scope");
    }

    [Fact]
    public void Merge_ConsolidatedPlan_AppendsDiscussionSection()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ConsolidatedPlan, "## consolidated multi-role notes");

        var merged = PlanContextRenderer.Merge(null, pipeline);

        merged.Should().Contain("Multi-Role Discussion");
        merged.Should().Contain("consolidated multi-role notes");
    }
}
