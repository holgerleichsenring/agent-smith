using AgentSmith.Application.PipelineDataFlows;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>
/// p0129a: FixBug + AddFeature gain explicit Plan→Verify and Implementation→Verify
/// edges next to the wildcard. Documentation-only — wildcard still matches first;
/// real enforcement is post-D7.
/// </summary>
public sealed class VerifyPhaseDataFlowEdgesTests
{
    [Fact]
    public void FixBug_Edges_IncludePlanToVerifyWithPlanJsonAndPlanKeys()
    {
        var edges = new FixBugDataFlow().Edges;

        edges.Should().Contain(e =>
            e.FromPhaseStep == "Plan" && e.ToPhaseStep == "Verify"
            && e.ContextKeys.Contains("PlanJson")
            && e.ContextKeys.Contains("Plan"));
    }

    [Fact]
    public void FixBug_Edges_IncludeImplementationToVerifyWithDiffJsonAndCodeChanges()
    {
        var edges = new FixBugDataFlow().Edges;

        edges.Should().Contain(e =>
            e.FromPhaseStep == "Implementation" && e.ToPhaseStep == "Verify"
            && e.ContextKeys.Contains("DiffJson")
            && e.ContextKeys.Contains("CodeChanges"));
    }

    [Fact]
    public void FixBug_Edges_StillIncludeWildcard()
    {
        var edges = new FixBugDataFlow().Edges;

        edges.Should().Contain(e =>
            e.FromPhaseStep == "*" && e.ToPhaseStep == "*"
            && e.ContextKeys.Contains("*"));
    }

    [Fact]
    public void AddFeature_Edges_IncludePlanToVerifyAndImplementationToVerify()
    {
        var edges = new AddFeatureDataFlow().Edges;

        edges.Should().Contain(e =>
            e.FromPhaseStep == "Plan" && e.ToPhaseStep == "Verify");
        edges.Should().Contain(e =>
            e.FromPhaseStep == "Implementation" && e.ToPhaseStep == "Verify");
    }

    [Fact]
    public void AddFeature_Edges_StillIncludeWildcard()
    {
        new AddFeatureDataFlow().Edges
            .Should().Contain(e =>
                e.FromPhaseStep == "*" && e.ToPhaseStep == "*"
                && e.ContextKeys.Contains("*"));
    }
}
