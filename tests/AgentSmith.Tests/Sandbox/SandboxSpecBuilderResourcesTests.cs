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

    // 2026-10-25-c5a7a, 2026-10-25-ff3b: the installation switch decides, the project may opt back.
    [Theory]
    [InlineData(false, false, "unconfined")]
    [InlineData(false, true, "unconfined")]
    [InlineData(true, false, "hardened")]
    [InlineData(true, true, "root")]
    public void SandboxSpecBuilder_HardeningAndRunAsRoot_ResolveTheProfile(bool hardening, bool runAsRoot, string expected)
    {
        var sut = new SandboxSpecBuilder(new StubSandboxResourceResolver(), new StubAgentImageResolver(),
            Microsoft.Extensions.Options.Options.Create(new SandboxGlobalConfig { HardeningEnabled = hardening }));
        var project = new ResolvedProject { Sandbox = new SandboxConfig { RunAsRoot = runAsRoot } };

        sut.Build(project, "csharp", "code").SecurityContext.Should().Be(expected switch
        {
            "hardened" => SandboxSecurityContext.Hardened,
            "root" => SandboxSecurityContext.Root,
            _ => SandboxSecurityContext.Unconfined,
        });
    }

    [Fact]
    public void Build_WithoutLanguage_StillPopulatesResources()
    {
        var sut = new SandboxSpecBuilder(new StubSandboxResourceResolver(), new StubAgentImageResolver());

        var spec = sut.Build(new ResolvedProject(), language: null, pipelineName: "code");

        spec.Resources.Should().Be(ResourceLimits.Default);
    }
}
