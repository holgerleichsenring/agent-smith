using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

public sealed class AgentImageResolverTests
{
    [Fact]
    public void Resolve_GlobalVersionSet_ReturnsRegistryNameVersion()
    {
        var sut = new AgentImageResolver(Options.Create(new SandboxGlobalConfig
        {
            AgentRegistry = "holgerleichsenring",
            AgentVersion = "0.48.0"
        }));

        sut.Resolve(new ResolvedProject())
            .Should().Be("holgerleichsenring/agent-smith-sandbox-agent:0.48.0");
    }

    [Fact]
    public void Resolve_PerProjectRegistryOverride_WinsOverGlobal()
    {
        var sut = new AgentImageResolver(Options.Create(new SandboxGlobalConfig
        {
            AgentRegistry = "holgerleichsenring",
            AgentVersion = "0.48.0"
        }));
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { AgentRegistry = "corp-mirror" }
        };

        sut.Resolve(project).Should().Be("corp-mirror/agent-smith-sandbox-agent:0.48.0");
    }

    [Fact]
    public void Resolve_PerProjectVersionOverride_WinsOverGlobal()
    {
        var sut = new AgentImageResolver(Options.Create(new SandboxGlobalConfig
        {
            AgentRegistry = "holgerleichsenring",
            AgentVersion = "0.48.0"
        }));
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { AgentVersion = "0.49.0-beta" }
        };

        sut.Resolve(project).Should().Be("holgerleichsenring/agent-smith-sandbox-agent:0.49.0-beta");
    }

    [Fact]
    public void Resolve_VersionMissingEverywhere_ThrowsClearMessage()
    {
        var sut = new AgentImageResolver(Options.Create(new SandboxGlobalConfig
        {
            AgentRegistry = "holgerleichsenring",
            AgentVersion = ""
        }));

        var act = () => sut.Resolve(new ResolvedProject());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*sandbox.agent_version*");
    }

    [Fact]
    public void Resolve_EmptyRegistry_OmitsPrefix()
    {
        var sut = new AgentImageResolver(Options.Create(new SandboxGlobalConfig
        {
            AgentRegistry = "",
            AgentVersion = "1.0.0"
        }));

        sut.Resolve(new ResolvedProject()).Should().Be("agent-smith-sandbox-agent:1.0.0");
    }
}
