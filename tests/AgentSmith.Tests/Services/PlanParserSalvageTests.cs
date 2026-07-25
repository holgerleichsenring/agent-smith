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
    public void SalvageProse_ValidJsonRejectedByStrict_ExtractsSummaryAndSteps_NotRawJson()
    {
        // p0376: a {summary,steps} JSON the strict schema rejected (no scope/open_questions/
        // status) must surface the prose summary + steps — never dump the raw JSON object.
        var json = """
            {"summary":"Migrate MediatR to Mediator and MassTransit to Wolverine.",
             "steps":[{"order":1,"description":"Create feature branch","target_file":"N/A","change_type":"Create"},
                      {"order":2,"description":"Swap package refs","target_file":"*.csproj","change_type":"Modify"}]}
            """;

        var plan = _parser.SalvageProse(json);

        plan.Summary.Should().Be("Migrate MediatR to Mediator and MassTransit to Wolverine.");
        plan.Summary.Should().NotContain("{").And.NotContain("\"steps\"");
        plan.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void SalvageProse_TruncatedJson_RecoversSummaryViaRegex_NoRawJson()
    {
        // Cut off at MaxOutputTokens — not parseable, but the summary is recoverable.
        var truncated = """{"summary":"Migrate the messaging stack to Wolverine.","steps":[{"order":1,"desc""";

        var plan = _parser.SalvageProse(truncated);

        plan.Summary.Should().Be("Migrate the messaging stack to Wolverine.");
        plan.Summary.Should().NotContain("{");
    }

    [Fact]
    public void SalvageProse_UnparseableJsonNoSummary_NeverSurfacesRawJson()
    {
        var blob = """{"unexpected":"shape","nested":{"x":1}}""";

        var plan = _parser.SalvageProse(blob);

        plan.Summary.Should().NotContain("{").And.NotContain("unexpected");
        plan.Summary.Should().Contain("unparseable");
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
