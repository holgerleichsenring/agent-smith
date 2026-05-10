using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Domain.Entities;
using AgentSmith.Tests.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class PlanParserStrictModeTests
{
    private static PlanOutputValidator NewValidator() => new(new JsonSchemaLoader());

    [Fact]
    public void ParseStrict_ValidPlanJson_ReturnsTypedPlan()
    {
        var json = PlanFixtures.ValidNeedsUserInput();

        var result = PlanParser.ParseStrict(json, NewValidator());

        result.Validation.IsValid.Should().BeTrue();
        result.Plan.Should().NotBeNull();
        result.Plan!.Status.Should().Be(PlanStatus.NeedsUserInput);
        result.Plan.OpenQuestions.Should().HaveCount(1);
    }

    [Fact]
    public void ParseStrict_InvalidJson_ReturnsValidationFailure()
    {
        var result = PlanParser.ParseStrict("not json", NewValidator());

        result.Plan.Should().BeNull();
        result.Validation.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ParseStrict_SchemaViolation_ReturnsValidationFailureWithJsonPointer()
    {
        var bad = PlanFixtures.Build(stepReasonChars: 400);

        var result = PlanParser.ParseStrict(bad, NewValidator());

        result.Plan.Should().BeNull();
        result.Validation.ErrorMessage.Should().Contain("/steps/0/reason");
    }

    [Fact]
    public void Parse_LegacyPath_StillWorks()
    {
        var legacy = """
        {
          "summary": "old shape",
          "steps": [
            {"order": 1, "description": "do", "target_file": "src/F.cs", "change_type": "Modify"}
          ]
        }
        """;

        var plan = PlanParser.Parse("Claude", legacy);

        plan.Summary.Should().Be("old shape");
        plan.Steps.Should().HaveCount(1);
        plan.Status.Should().Be(PlanStatus.Complete);
    }
}
