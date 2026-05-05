using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Models;

public class StepValidateTests
{
    [Fact]
    public void Validate_RunWithCommand_IsValid()
    {
        var step = new Step(1, Guid.NewGuid(), StepKind.Run, Command: "echo");
        step.Validate().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RunWithEmptyCommand_IsInvalid()
    {
        var step = new Step(1, Guid.NewGuid(), StepKind.Run, Command: "");
        var (isValid, error) = step.Validate();
        isValid.Should().BeFalse();
        error.Should().Contain("Command");
    }

    [Fact]
    public void Validate_ShutdownAlwaysValid()
    {
        Step.Shutdown(Guid.NewGuid()).Validate().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReadFileWithoutPath_IsInvalid()
    {
        var step = new Step(1, Guid.NewGuid(), StepKind.ReadFile);
        var (isValid, error) = step.Validate();
        isValid.Should().BeFalse();
        error.Should().Contain("Path");
    }

    [Fact]
    public void Validate_ReadFileWithPath_IsValid()
    {
        var step = new Step(1, Guid.NewGuid(), StepKind.ReadFile, Path: "/work/foo.cs");
        step.Validate().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WriteFileWithEmptyContent_IsValid()
    {
        var step = new Step(1, Guid.NewGuid(), StepKind.WriteFile,
            Path: "/work/foo.txt", Content: "");
        step.Validate().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WriteFileWithNullContent_IsInvalid()
    {
        var step = new Step(1, Guid.NewGuid(), StepKind.WriteFile, Path: "/work/foo.txt");
        var (isValid, error) = step.Validate();
        isValid.Should().BeFalse();
        error.Should().Contain("Content");
    }

    [Fact]
    public void Validate_ListFilesWithoutPath_IsInvalid()
    {
        var step = new Step(1, Guid.NewGuid(), StepKind.ListFiles);
        var (isValid, error) = step.Validate();
        isValid.Should().BeFalse();
        error.Should().Contain("Path");
    }
}
