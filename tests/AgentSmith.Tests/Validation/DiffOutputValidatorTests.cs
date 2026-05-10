using System.Text.Json;
using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

public sealed class DiffOutputValidatorTests
{
    private static DiffOutputValidator NewValidator() => new(new JsonSchemaLoader());

    [Fact]
    public void Validate_ValidDiff_ReturnsOk()
    {
        var result = NewValidator().Validate(PlanFixtures.ValidDiff());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_OperationOutsideEnum_ReturnsFailure()
    {
        var bad = JsonSerializer.Serialize(new
        {
            changes = new[] { new { file = "x", operation = "rename", summary = "s", patch = "" } },
            tests_added = Array.Empty<object>(),
            tests_modified = Array.Empty<object>(),
            build_status = "ok",
            test_status = "ok"
        });

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ChangeSummaryOver200Chars_ReturnsFailure()
    {
        var bad = JsonSerializer.Serialize(new
        {
            changes = new[]
            {
                new
                {
                    file = "x",
                    operation = "modify",
                    summary = new string('s', 220),
                    patch = ""
                }
            },
            tests_added = Array.Empty<object>(),
            tests_modified = Array.Empty<object>(),
            build_status = "ok",
            test_status = "ok"
        });

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("/changes/0/summary");
    }

    [Fact]
    public void Validate_BuildStatusOutsideEnum_ReturnsFailure()
    {
        var bad = JsonSerializer.Serialize(new
        {
            changes = Array.Empty<object>(),
            tests_added = Array.Empty<object>(),
            tests_modified = Array.Empty<object>(),
            build_status = "rolling",
            test_status = "ok"
        });

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_MissingChanges_ReturnsFailure()
    {
        var bad = JsonSerializer.Serialize(new
        {
            tests_added = Array.Empty<object>(),
            tests_modified = Array.Empty<object>(),
            build_status = "ok",
            test_status = "ok"
        });

        var result = NewValidator().Validate(bad);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NotJson_ReturnsFailure()
    {
        var result = NewValidator().Validate("not json");

        result.IsValid.Should().BeFalse();
    }
}
