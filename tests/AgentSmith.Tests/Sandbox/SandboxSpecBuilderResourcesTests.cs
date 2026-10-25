using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

public sealed class SandboxSpecBuilderResourcesTests
{
    [Fact]
    public void Build_PopulatesResourcesFromResolver()
    {
        var resolverResult = new ResourceLimits("750m", "3000m", "1Gi", "8Gi");
        var sut = new SandboxSpecBuilder(new StubSandboxResourceResolver(resolverResult), new StubAgentImageResolver());

        var spec = sut.Build(new ResolvedProject(), language: "csharp", pipelineName: "code");

        spec.Resources.Should().BeSameAs(resolverResult);
    }

    // 2026-10-25-c5a7a
    [Fact]
    public void SandboxSpecBuilder_ProjectRunAsRoot_ReachesTheSpec()
    {
        var sut = new SandboxSpecBuilder(new StubSandboxResourceResolver(), new StubAgentImageResolver());
        var root = new ResolvedProject { Sandbox = new SandboxConfig { RunAsRoot = true } };

        sut.Build(root, "csharp", "code").SecurityContext.Should().Be(SandboxSecurityContext.Root);
        sut.Build(new ResolvedProject(), "csharp", "code").SecurityContext.Should().Be(SandboxSecurityContext.Hardened);
    }

    [Fact]
    public void Build_WithoutLanguage_StillPopulatesResources()
    {
        var sut = new SandboxSpecBuilder(new StubSandboxResourceResolver(), new StubAgentImageResolver());

        var spec = sut.Build(new ResolvedProject(), language: null, pipelineName: "code");

        spec.Resources.Should().Be(ResourceLimits.Default);
    }
}
