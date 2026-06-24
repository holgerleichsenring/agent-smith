using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Orchestrator;

public sealed class OrchestratorImageResolverTests
{
    [Fact]
    public void Resolve_GlobalRegistryAndVersion_BuildsFullyQualifiedImage()
    {
        var sut = new OrchestratorImageResolver(Options.Create(new OrchestratorGlobalConfig
        {
            Registry = "holgerleichsenring",
            Version = "0.48.0"
        }));

        sut.Resolve(new ResolvedProject()).Should().Be("holgerleichsenring/agentsmith-cli:0.48.0");
    }

    // p0281c: the orchestrator image is a deployment-wide pin, not a project concern.
    // A per-project orchestrator.registry/version is accepted-but-IGNORED (and warned).
    [Fact]
    public void Resolve_ProjectImageOverride_IsIgnored_GlobalWins()
    {
        var sut = new OrchestratorImageResolver(Options.Create(new OrchestratorGlobalConfig
        {
            Registry = "holgerleichsenring",
            Version = "0.48.0"
        }));
        var project = new ResolvedProject
        {
            Orchestrator = new OrchestratorConfig { Registry = "corp-mirror", Version = "0.49.0-beta" }
        };

        sut.Resolve(project).Should().Be("holgerleichsenring/agentsmith-cli:0.48.0");
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
