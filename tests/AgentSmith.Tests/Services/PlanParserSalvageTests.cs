using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0340: a planner that returns prose (or JSON missing optional fields) must still
// yield a PRESENT plan, so the Approval / open-questions gate is not silently empty
// (which is what disabled the clarification gate on the 2026-07-14 run).
public sealed class PlanParserSalvageTests
{
    private readonly AgentSmith.Application.Services.Prompts.PlanParser _parser =
        TolerantJsonParserFactory.CreatePlan();

    [Fact]
    public void PlanParser_ProsePlan_SalvagedIntoSteps()
    {
        var prose = """
            Plan for ticket 19106
            1. Inspect the solution and inventory MediatR usages.
            2. Replace MediatR with Mediator in the server.
            3. Migrate MassTransit to Wolverine, preserving topology.
            """;

        var plan = _parser.SalvageProse(prose);

        plan.Should().NotBeNull();
        plan.Steps.Should().HaveCount(3);
        plan.Steps[0].Description.Should().Contain("Inspect the solution");
        plan.Steps[2].Description.Should().Contain("Migrate MassTransit");
    }

    [Fact]
    public void PlanParser_JsonMissingOptionalField_DoesNotThrow()
    {
        // legacy JSON carrying a summary but no steps/decisions array — tolerant,
        // never a throw; yields a present plan with an empty step list.
        var json = """{ "summary": "just a summary, no steps array" }""";

        var act = () => _parser.Parse("test-provider", json);

        act.Should().NotThrow();
        _parser.Parse("test-provider", json).Steps.Should().BeEmpty();
    }
}
