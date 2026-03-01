using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public sealed class CommandResultTests
{
    [Fact]
    public void Ok_ReturnsSuccessResult()
    {
        var result = CommandResult.Ok("done");

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("done");
        result.Exception.Should().BeNull();
        result.InsertNext.Should().BeNull();
    }

    [Fact]
    public void Fail_ReturnsFailureResult()
    {
        var result = CommandResult.Fail("broken");

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("broken");
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Fail_WithException_IncludesException()
    {
        var ex = new InvalidOperationException("boom");
        var result = CommandResult.Fail("broken", ex);

        result.IsSuccess.Should().BeFalse();
        result.Exception.Should().BeSameAs(ex);
    }

    [Fact]
    public void OkAndContinueWith_SetsInsertNext()
    {
        var result = CommandResult.OkAndContinueWith(
            "triaged", "SkillRoundCommand:architect:1", "ConvergenceCheckCommand");

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("triaged");
        result.InsertNext.Should().HaveCount(2);
        result.InsertNext![0].Should().Be("SkillRoundCommand:architect:1");
        result.InsertNext[1].Should().Be("ConvergenceCheckCommand");
    }

    [Fact]
    public void OkAndContinueWith_NoCommands_InsertNextIsNull()
    {
        var result = CommandResult.OkAndContinueWith("done");

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().BeNull();
    }

    [Fact]
    public void WithRecord_CanSetStepInfo()
    {
        var result = CommandResult.Fail("error") with
        {
            FailedStep = 3,
            TotalSteps = 10,
            StepName = "Running tests"
        };

        result.FailedStep.Should().Be(3);
        result.TotalSteps.Should().Be(10);
        result.StepName.Should().Be("Running tests");
    }
}
