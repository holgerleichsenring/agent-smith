using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

public sealed class OutcomeClassifierTests
{
    private readonly OutcomeClassifier _classifier = new();

    [Fact]
    public void Classify_ResponsePresentParseOkValidationOk_ReturnsOk()
    {
        var input = new ClassificationInput
        {
            ResponsePresent = true,
            ParseSuccess = true,
            ValidationSuccess = true
        };

        _classifier.Classify(input).Should().Be(SkillCallOutcome.Ok);
    }

    [Fact]
    public void Classify_LimitHitWithResponse_ReturnsIncomplete()
    {
        var input = new ClassificationInput
        {
            ResponsePresent = true,
            ParseSuccess = true,
            ValidationSuccess = true,
            LimitHit = LimitDecision.Cap(LimitDecisionKind.CappedTokens, "input over cap")
        };

        _classifier.Classify(input).Should().Be(SkillCallOutcome.Incomplete);
    }

    [Fact]
    public void Classify_ParseFailure_ReturnsFailedParse()
    {
        var input = new ClassificationInput
        {
            ResponsePresent = true,
            ParseSuccess = false,
            ValidationSuccess = true
        };

        _classifier.Classify(input).Should().Be(SkillCallOutcome.FailedParse);
    }

    [Fact]
    public void Classify_ValidationFailure_ReturnsFailedValidation()
    {
        var input = new ClassificationInput
        {
            ResponsePresent = true,
            ParseSuccess = true,
            ValidationSuccess = false
        };

        _classifier.Classify(input).Should().Be(SkillCallOutcome.FailedValidation);
    }

    [Fact]
    public void Classify_CaughtException_ReturnsFailedRuntime()
    {
        var input = new ClassificationInput
        {
            ResponsePresent = false,
            ParseSuccess = false,
            ValidationSuccess = false,
            CaughtException = new InvalidOperationException("network error")
        };

        _classifier.Classify(input).Should().Be(SkillCallOutcome.FailedRuntime);
    }

    [Fact]
    public void Classify_LimitHitButNoResponse_ReturnsFailedRuntime()
    {
        var input = new ClassificationInput
        {
            ResponsePresent = false,
            ParseSuccess = false,
            ValidationSuccess = false,
            LimitHit = LimitDecision.Cap(LimitDecisionKind.CappedTime, "wall clock")
        };

        _classifier.Classify(input).Should().Be(SkillCallOutcome.FailedRuntime);
    }
}
