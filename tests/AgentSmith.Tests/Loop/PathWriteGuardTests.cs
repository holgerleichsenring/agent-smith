using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Providers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Loop;

public sealed class PathWriteGuardTests
{
    private const string RepoRoot = "/work";

    private static PathWriteGuard Build()
    {
        var gitIgnore = new Mock<IGitIgnoreResolver>();
        gitIgnore.Setup(g => g.IsIgnored(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var readGuard = new PathReadGuard(gitIgnore.Object);
        return new PathWriteGuard(readGuard);
    }

    [Fact]
    public void AssertWritable_PlanPhase_ReturnsErrorWriteForbidden()
    {
        var guard = Build();

        var result = guard.AssertWritable("/work/src/main.cs", RepoRoot, SkillExecutionPhase.Plan, contextName: null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.WriteForbiddenInPhase);
    }

    [Fact]
    public void AssertWritable_ImplementationPhase_PathInsideRepo_ReturnsOk()
    {
        var guard = Build();

        var result = guard.AssertWritable("/work/src/main.cs", RepoRoot, SkillExecutionPhase.Implementation, contextName: null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssertWritable_BootstrapPhase_ContextYamlPath_ReturnsOk()
    {
        var guard = Build();

        var result = guard.AssertWritable("/work/.agentsmith/context.yaml", RepoRoot, SkillExecutionPhase.Bootstrap, contextName: null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssertWritable_BootstrapPhase_OtherPath_ReturnsErrorBootstrapFiles()
    {
        var guard = Build();

        var result = guard.AssertWritable("/work/src/main.cs", RepoRoot, SkillExecutionPhase.Bootstrap, contextName: null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.NotInBootstrapFiles);
    }

    [Fact]
    public void AssertWritable_VerifyPhase_ReturnsErrorWriteForbidden()
    {
        var guard = Build();

        var result = guard.AssertWritable("/work/src/main.cs", RepoRoot, SkillExecutionPhase.Verify, contextName: null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.WriteForbiddenInPhase);
    }

    [Fact]
    public void BootstrapPathWriteGuard_PerContext_AcceptsTargetContextMetaDir()
    {
        // p0161d: a Bootstrap-phase guard scoped to context "server" must accept
        // .agentsmith/contexts/server/{context.yaml, coding-principles.md} and
        // nothing else.
        var guard = Build();

        guard.AssertWritable("/work/.agentsmith/contexts/server/context.yaml", RepoRoot, SkillExecutionPhase.Bootstrap, "server")
            .IsSuccess.Should().BeTrue();
        guard.AssertWritable("/work/.agentsmith/contexts/server/coding-principles.md", RepoRoot, SkillExecutionPhase.Bootstrap, "server")
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void BootstrapPathWriteGuard_RejectsForeignContextPath()
    {
        // p0161d: when the round writes for context "server", attempting to write
        // into context "client" must be rejected by the per-context guard.
        var guard = Build();

        var result = guard.AssertWritable("/work/.agentsmith/contexts/client/context.yaml", RepoRoot, SkillExecutionPhase.Bootstrap, "server");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.NotInBootstrapFiles);
    }

    [Fact]
    public void BootstrapPathWriteGuard_RejectsFlatRootPath()
    {
        // p0161d: the flat .agentsmith/context.yaml legacy path is rejected for
        // per-context rounds — the only legal writes live under MetaDirFor(name).
        var guard = Build();

        var result = guard.AssertWritable("/work/.agentsmith/context.yaml", RepoRoot, SkillExecutionPhase.Bootstrap, "server");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.NotInBootstrapFiles);
    }
}
