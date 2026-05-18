using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Orchestrator;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Orchestrator;

public sealed class OrchestratorResourceResolverTests
{
    private static readonly ResourceLimits GlobalDefault = new(
        cpuRequest: "100m",
        cpuLimit: "500m",
        memoryRequest: "256Mi",
        memoryLimit: "1Gi");

    private static readonly ResourceLimits ProjectOverride = new(
        cpuRequest: "1000m",
        cpuLimit: "4000m",
        memoryRequest: "2Gi",
        memoryLimit: "8Gi");

    [Fact]
    public void Resolve_ProjectHasResources_ReturnsProjectResources()
    {
        var options = Options.Create(new JobSpawnerOptions { Resources = GlobalDefault });
        var sut = new OrchestratorResourceResolver(options);
        var project = new ResolvedProject
        {
            Orchestrator = new OrchestratorConfig { Resources = ProjectOverride }
        };

        sut.Resolve(project).Should().BeEquivalentTo(ProjectOverride);
    }

    [Fact]
    public void Resolve_ProjectOrchestratorNullResourcesNull_ReturnsGlobalDefaults()
    {
        var options = Options.Create(new JobSpawnerOptions { Resources = GlobalDefault });
        var sut = new OrchestratorResourceResolver(options);
        var project = new ResolvedProject
        {
            Orchestrator = new OrchestratorConfig { Resources = null }
        };

        sut.Resolve(project).Should().BeEquivalentTo(GlobalDefault);
    }

    [Fact]
    public void Resolve_ProjectOrchestratorNull_ReturnsGlobalDefaults()
    {
        var options = Options.Create(new JobSpawnerOptions { Resources = GlobalDefault });
        var sut = new OrchestratorResourceResolver(options);

        sut.Resolve(new ResolvedProject()).Should().BeEquivalentTo(GlobalDefault);
    }
}
