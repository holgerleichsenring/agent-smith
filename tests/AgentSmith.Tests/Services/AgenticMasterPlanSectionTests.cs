using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>p0276: the master renders the pre-generated, approved plan into its
/// body so it executes that plan; absent plan → empty section (the master plans
/// itself, back-compat).</summary>
public sealed class AgenticMasterPlanSectionTests
{
    [Fact]
    public void BuildPlanSection_WithPlan_RendersSummaryAndOrderedSteps()
    {
        var plan = new Plan(
            "Fix the controller",
            new[]
            {
                new PlanStep(2, "Update ProducesResponseType", null, "modify"),
                new PlanStep(1, "Return 201 from CreateApplication", null, "modify"),
            },
            rawResponse: "{}");

        var section = AgenticMasterHandler.BuildPlanSection(plan);

        section.Should().Contain("Approved plan");
        section.Should().Contain("Fix the controller");
        // Ordered by step order, not insertion order.
        section.Should().Contain("[1] modify: Return 201 from CreateApplication");
        section.IndexOf("[1] modify", System.StringComparison.Ordinal)
            .Should().BeLessThan(section.IndexOf("[2] modify", System.StringComparison.Ordinal));
    }

    [Fact]
    public void BuildPlanSection_NullOrEmpty_ReturnsEmpty()
    {
        AgenticMasterHandler.BuildPlanSection(null).Should().BeEmpty();
        AgenticMasterHandler.BuildPlanSection(
            new Plan("s", System.Array.Empty<PlanStep>(), "{}")).Should().BeEmpty();
    }
}
