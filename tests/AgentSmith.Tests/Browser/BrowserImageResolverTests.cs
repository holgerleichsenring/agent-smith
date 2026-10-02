using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AgentSmith.Tests.Browser;

/// <summary>2026-10-01-283de: the browser image is pulled beside the carrier — same registry, same tag.</summary>
public sealed class BrowserImageResolverTests
{
    private static readonly IAgentVersionResolver Versions = Mock.Of<IAgentVersionResolver>(v =>
        v.Resolve(It.IsAny<ResolvedProject>()) == new AgentVersionChoice("0.153.0", "0.153.0", false));

    [Fact]
    public void BrowserImageResolver_DefaultRegistry_NamesSandboxBrowserAtTheAgentVersion()
    {
        var resolver = new BrowserImageResolver(Options.Create(new SandboxGlobalConfig()), Versions);

        resolver.Resolve(new ResolvedProject { Name = "p" })
            .Should().Be("holgerleichsenring/agent-smith-sandbox-browser:0.153.0");
    }

    [Fact]
    public void BrowserImageResolver_ProjectRegistry_WinsOverTheGlobalOne()
    {
        var resolver = new BrowserImageResolver(Options.Create(new SandboxGlobalConfig()), Versions);
        var project = new ResolvedProject { Name = "p", Sandbox = new SandboxConfig { AgentRegistry = "mirror.example" } };

        resolver.Resolve(project).Should().Be("mirror.example/agent-smith-sandbox-browser:0.153.0");
    }
}
