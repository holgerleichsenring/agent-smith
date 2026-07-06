using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

public sealed class PlanOutputValidatorTests
{
    private static PlanOutputValidator NewValidator() => new(new JsonSchemaLoader());

    [Fact]
    public void Validate_ValidPlanComplete_ReturnsOk()
    {
        var result = NewValidator().Validate(PlanFixtures.ValidComplete());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidPlanNeedsUserInput_ReturnsOk()
    {
        var result = NewValidator().Validate(PlanFixtures.ValidNeedsUserInput());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StatusCompleteWithOpenQuestions_ReturnsFailureCitingConstraint()
    {
        var bad = PlanFixtures.Build(status: "complete", openQuestionCount: 2);

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("plan");
    }

    [Fact]
    public void Validate_StatusNeedsUserInputWithEmptyOpenQuestions_ReturnsFailure()
    {
        var bad = PlanFixtures.Build(status: "needs_user_input", openQuestionCount: 0);

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_SummaryWithin600Chars_ReturnsOk()
    {
        // p0318: the 200-char cap tipped valid plans into the legacy fallback
        // reproducibly; relaxed to 600 so a legitimate summary is not rejected.
        var ok = PlanFixtures.Build(summaryChars: 250);

        var result = NewValidator().Validate(ok);

        result.IsValid.Should().BeTrue(result.ErrorMessage);
    }

    [Fact]
    public void Validate_SummaryOver600Chars_ReturnsFailureWithJsonPointer()
    {
        var bad = PlanFixtures.Build(summaryChars: 650);

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("/summary");
    }

    [Fact]
    public void Validate_StepReasonOver300Chars_ReturnsFailureWithJsonPointer()
    {
        var bad = PlanFixtures.Build(stepReasonChars: 400);

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("/steps/0/reason");
    }

    [Fact]
    public void Validate_MissingRequiredField_ReturnsFailure()
    {
        var bad = """{ "summary": "x" }""";

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NotJson_ReturnsFailure()
    {
        var result = NewValidator().Validate("not json at all");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not valid JSON");
    }
}
