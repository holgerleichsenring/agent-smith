using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Providers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Loop;

public sealed class PathWriteGuardTests
{
    private const string RepoRoot = "/work";

    private static PathWriteGuard Build(SkillExecutionPhase phase)
    {
        var gitIgnore = new Mock<IGitIgnoreResolver>();
        gitIgnore.Setup(g => g.IsIgnored(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var readGuard = new PathReadGuard(gitIgnore.Object, () => RepoRoot);
        return new PathWriteGuard(readGuard, phase);
    }

    [Fact]
    public void AssertWritable_PlanPhase_ReturnsErrorWriteForbidden()
    {
        var guard = Build(SkillExecutionPhase.Plan);

        var result = guard.AssertWritable("/work/src/main.cs");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.WriteForbiddenInPhase);
    }

    [Fact]
    public void AssertWritable_ImplementationPhase_PathInsideRepo_ReturnsOk()
    {
        var guard = Build(SkillExecutionPhase.Implementation);

        var result = guard.AssertWritable("/work/src/main.cs");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssertWritable_BootstrapPhase_ContextYamlPath_ReturnsOk()
    {
        var guard = Build(SkillExecutionPhase.Bootstrap);

        var result = guard.AssertWritable("/work/.agentsmith/context.yaml");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssertWritable_BootstrapPhase_OtherPath_ReturnsErrorBootstrapFiles()
    {
        var guard = Build(SkillExecutionPhase.Bootstrap);

        var result = guard.AssertWritable("/work/src/main.cs");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.NotInBootstrapFiles);
    }

    [Fact]
    public void AssertWritable_VerifyPhase_ReturnsErrorWriteForbidden()
    {
        var guard = Build(SkillExecutionPhase.Verify);

        var result = guard.AssertWritable("/work/src/main.cs");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.WriteForbiddenInPhase);
    }
}
