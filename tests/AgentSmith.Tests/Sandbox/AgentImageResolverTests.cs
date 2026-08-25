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
        Sut("holgerleichsenring", "0.48.0").Resolve(new ResolvedProject())
            .Should().Be("holgerleichsenring/agent-smith-sandbox-agent:0.48.0");
    }

    [Fact]
    public void Resolve_PerProjectRegistryOverride_WinsOverGlobal()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { AgentRegistry = "corp-mirror" }
        };

        Sut("holgerleichsenring", "0.48.0").Resolve(project)
            .Should().Be("corp-mirror/agent-smith-sandbox-agent:0.48.0");
    }

    [Fact]
    public void Resolve_PerProjectVersionOverride_WinsOverGlobal()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { AgentVersion = "0.49.0-beta" }
        };

        Sut("holgerleichsenring", "0.48.0").Resolve(project)
            .Should().Be("holgerleichsenring/agent-smith-sandbox-agent:0.49.0-beta");
    }

    [Fact]
    public void Resolve_VersionMissingEverywhere_ThrowsClearMessage()
    {
        var act = () => Sut("holgerleichsenring", "", serverVersion: null).Resolve(new ResolvedProject());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*sandbox.agent_version*");
    }

    [Fact]
    public void Resolve_EmptyRegistry_OmitsPrefix()
    {
        Sut(registry: "", version: "1.0.0").Resolve(new ResolvedProject())
            .Should().Be("agent-smith-sandbox-agent:1.0.0");
    }

    // 2026-08-25-0d01: nothing declares a version, so the tag follows the server. The
    // reference an operator would previously have had to keep aligned by hand.
    [Fact]
    public void Resolve_NoVersionDeclared_NamesTheServersOwnRelease()
    {
        Sut("holgerleichsenring", version: "", serverVersion: "0.135.0").Resolve(new ResolvedProject())
            .Should().Be("holgerleichsenring/agent-smith-sandbox-agent:0.135.0");
    }

    private static AgentImageResolver Sut(string registry, string version, string? serverVersion = "0.135.0")
    {
        var global = Options.Create(new SandboxGlobalConfig
        {
            AgentRegistry = registry,
            AgentVersion = version
        });
        return new AgentImageResolver(
            global, new AgentVersionResolver(global, new BuildIdentity("abc123", serverVersion)));
    }
}
