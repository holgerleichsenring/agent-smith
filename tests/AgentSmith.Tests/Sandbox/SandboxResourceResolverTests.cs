using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

public sealed class SandboxResourceResolverTests
{
    [Fact]
    public void Resolve_ProjectHasResourceBlock_ReturnsProjectResources()
    {
        var projectResources = new ResourceLimits("500m", "2000m", "1Gi", "4Gi");
        var project = new ResolvedProject { Sandbox = new SandboxConfig { Resources = projectResources } };
        var sut = new SandboxResourceResolver(Options.Create(new SandboxOptions()));

        var resolved = sut.Resolve(project);

        resolved.Should().BeSameAs(projectResources);
    }

    [Fact]
    public void Resolve_ProjectSandboxResourcesNull_ReturnsGlobalDefaults()
    {
        var project = new ResolvedProject { Sandbox = new SandboxConfig { Resources = null } };
        var sut = new SandboxResourceResolver(Options.Create(new SandboxOptions
        {
            CpuRequest = "300m", CpuLimit = "1500m", MemoryRequest = "768Mi", MemoryLimit = "3Gi"
        }));

        var resolved = sut.Resolve(project);

        resolved.Should().Be(new ResourceLimits("300m", "1500m", "768Mi", "3Gi"));
    }

    [Fact]
    public void Resolve_ProjectSandboxNull_ReturnsGlobalDefaults()
    {
        var project = new ResolvedProject { Sandbox = null };
        var sut = new SandboxResourceResolver(Options.Create(new SandboxOptions()));

        var resolved = sut.Resolve(project);

        resolved.Should().Be(ResourceLimits.Default);
    }
}
