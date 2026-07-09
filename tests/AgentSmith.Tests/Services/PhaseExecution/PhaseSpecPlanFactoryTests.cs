using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services.PhaseExecution;

/// <summary>
/// p0315d: the phase spec's ordered steps become the approved Plan the master
/// executes — rendered through the SAME BuildPlanSection contract the p0276
/// GeneratePlan path uses ("Approved plan — execute this"), so the pinned
/// coding-agent-master skill drives step-by-step spec execution unchanged.
/// </summary>
public sealed class PhaseSpecPlanFactoryTests
{
    private const string Yaml =
        """
        phase: p9999
        goal: "Add a widget endpoint to the sample service"
        steps:
          - id: impl
            action: "Add the widget endpoint + handler"
          - id: tests
            action: "Cover the endpoint with a contract test"
        done:
          - "GET /widget returns the widget"
        """;

    [Fact]
    public void Build_SpecSteps_BecomeOrderedPlanSteps()
    {
        var plan = new PhaseSpecPlanFactory().Build(Draft());

        plan.Summary.Should().Be("Add a widget endpoint to the sample service");
        plan.Steps.Should().HaveCount(2);
        plan.Steps[0].Order.Should().Be(1);
        plan.Steps[0].Description.Should().Be("impl: Add the widget endpoint + handler");
        plan.Steps[1].Order.Should().Be(2);
        plan.Steps[1].Description.Should().Be("tests: Cover the endpoint with a contract test");
    }

    [Fact]
    public void Build_PlanRendersThroughApprovedPlanSection()
    {
        var plan = new PhaseSpecPlanFactory().Build(Draft());

        var section = AgenticMasterHandler.BuildPlanSection(plan);

        section.Should().Contain("Approved plan — execute this",
            "the spec's steps must reach the pinned master via the existing approved-plan contract");
        section.Should().Contain("[1] implement: impl: Add the widget endpoint + handler");
        section.Should().Contain("[2] implement: tests: Cover the endpoint with a contract test");
    }

    private static PhaseDraft Draft() =>
        new("p9999", "Add a widget endpoint to the sample service", Yaml, []);
}
