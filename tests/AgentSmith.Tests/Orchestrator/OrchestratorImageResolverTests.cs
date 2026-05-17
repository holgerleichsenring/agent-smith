using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Orchestrator;

public sealed class OrchestratorImageResolverTests
{
    [Fact]
    public void Resolve_ProjectOverridesRegistryAndVersion_BuildsFullyQualifiedImage()
    {
        var sut = new OrchestratorImageResolver(Options.Create(new OrchestratorGlobalConfig
        {
            Registry = "holgerleichsenring",
            Version = "0.48.0"
        }));
        var project = new ResolvedProject
        {
            Orchestrator = new OrchestratorConfig
            {
                Registry = "corp-mirror",
                Version = "0.49.0-beta"
            }
        };

        sut.Resolve(project).Should().Be("corp-mirror/agentsmith-cli:0.49.0-beta");
    }

    [Fact]
    public void Resolve_ProjectOverridesVersionOnly_KeepsGlobalRegistry()
    {
        var sut = new OrchestratorImageResolver(Options.Create(new OrchestratorGlobalConfig
        {
            Registry = "holgerleichsenring",
            Version = "0.48.0"
        }));
        var project = new ResolvedProject
        {
            Orchestrator = new OrchestratorConfig { Version = "0.49.0-beta" }
        };

        sut.Resolve(project).Should().Be("holgerleichsenring/agentsmith-cli:0.49.0-beta");
    }

    [Fact]
    public void Resolve_BothEmpty_ThrowsInvalidOperationWithActionableMessage()
    {
        var sut = new OrchestratorImageResolver(Options.Create(new OrchestratorGlobalConfig
        {
            Registry = "",
            Version = ""
        }));

        var act = () => sut.Resolve(new ResolvedProject());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*orchestrator.version*");
    }

    [Fact]
    public void Resolve_GlobalRegistryEmpty_ProducesBareImageNameWithTag()
    {
        var sut = new OrchestratorImageResolver(Options.Create(new OrchestratorGlobalConfig
        {
            Registry = "",
            Version = "1.0.0"
        }));

        sut.Resolve(new ResolvedProject()).Should().Be("agentsmith-cli:1.0.0");
    }
}
