using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class ContextValidatorTests
{
    private readonly ContextValidator _sut = new();

    [Fact]
    public void Validate_ValidYaml_ReturnsSuccess()
    {
        // Arrange
        var yaml = """
            meta:
              project: my-app
              version: 1.0.0
              type: [api]
              purpose: "A test application"

            stack:
              runtime: .NET 8
              lang: C#

            arch:
              style: [CleanArch]
              layers:
                - Domain
                - Application

            quality:
              lang: english-only

            state:
              done: {}
              active: {}
            """;

        // Act
        var result = _sut.Validate(yaml);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingMeta_ReturnsError()
    {
        // Arrange
        var yaml = """
            stack:
              runtime: .NET 8
              lang: C#
            arch:
              style: [CleanArch]
              layers: [Domain]
            quality:
              lang: english-only
            state:
              done: {}
              active: {}
            """;

        // Act
        var result = _sut.Validate(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("meta"));
    }

    [Fact]
    public void Validate_MissingStackRuntime_ReturnsError()
    {
        // Arrange
        var yaml = """
            meta:
              project: my-app
              version: 1.0.0
              type: [api]
              purpose: "test"
            stack:
              lang: C#
            arch:
              style: [CleanArch]
              layers: [Domain]
            quality:
              lang: english-only
            state:
              done: {}
              active: {}
            """;

        // Act
        var result = _sut.Validate(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("stack.runtime"));
    }

    [Fact]
    public void Validate_InvalidYaml_ReturnsError()
    {
        // Arrange
        var yaml = "this is not: [valid: yaml: {{{}}}";

        // Act
        var result = _sut.Validate(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Validate_EmptyString_ReturnsError()
    {
        // Act
        var result = _sut.Validate("");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("YAML content is empty");
    }

    [Fact]
    public void Validate_MissingStateActive_ReturnsError()
    {
        // Arrange
        var yaml = """
            meta:
              project: my-app
              version: 1.0.0
              type: [api]
              purpose: "test"
            stack:
              runtime: .NET 8
              lang: C#
            arch:
              style: [CleanArch]
              layers: [Domain]
            quality:
              lang: english-only
            state:
              done: {}
            """;

        // Act
        var result = _sut.Validate(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("state.active"));
    }

    [Fact]
    public void Validate_MissingMultipleSections_ReturnsAllErrors()
    {
        // Arrange
        var yaml = """
            meta:
              project: test
              version: 1.0.0
              type: [api]
              purpose: "test"
            """;

        // Act
        var result = _sut.Validate(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("stack"));
        result.Errors.Should().Contain(e => e.Contains("arch"));
        result.Errors.Should().Contain(e => e.Contains("state"));
    }
}
