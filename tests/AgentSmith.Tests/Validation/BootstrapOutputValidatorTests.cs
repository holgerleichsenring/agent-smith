using System.Text.Json;
using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

public sealed class BootstrapOutputValidatorTests
{
    private static BootstrapOutputValidator NewValidator() => new(new JsonSchemaLoader());

    [Fact]
    public void Validate_ValidComplete_ReturnsOk()
    {
        var result = NewValidator().Validate(PlanFixtures.ValidBootstrapComplete());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidNeedsUserInput_ReturnsOk()
    {
        var result = NewValidator().Validate(PlanFixtures.ValidBootstrapNeedsUserInput());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StatusCompleteWithOpenQuestions_ReturnsFailure()
    {
        var bad = JsonSerializer.Serialize(new
        {
            status = "complete",
            files_written = new[] { new { path = "p", kind = "context_yaml" } },
            open_questions = new[] { new { id = "q1", question = "?", options = new[] { "a" } } }
        });

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_FilesWrittenEmptyOnComplete_ReturnsFailure()
    {
        var bad = JsonSerializer.Serialize(new
        {
            status = "complete",
            files_written = Array.Empty<object>(),
            open_questions = Array.Empty<object>()
        });

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NotJson_ReturnsFailure()
    {
        var result = NewValidator().Validate("garbage");

        result.IsValid.Should().BeFalse();
    }
}
