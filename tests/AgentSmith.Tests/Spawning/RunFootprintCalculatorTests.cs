using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// p0336: the footprint calculator sizes a run from the REAL toolchain groups the
/// remote inventory reports — a repo with two contexts (e.g. Server sdk8/sdk9)
/// yields two sandboxes, so a 3-repo project can correctly resolve to 4 sandboxes,
/// not 3. Limits are summed to the totals the budget gates on.
/// </summary>
public sealed class RunFootprintCalculatorTests
{
    [Fact]
    public async Task FootprintCalculator_ThreeRepoFourSandboxes_CountsRealToolchainGroups()
    {
        var project = Project("server", "client", "api");
        var language = new Mock<ISandboxLanguageResolver>();
        language.Setup(l => l.ResolveAllAsync(It.Is<RepoConnection>(r => r.Name == "server"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("sdk8"), Discovery("sdk9")]);
        language.Setup(l => l.ResolveAllAsync(It.Is<RepoConnection>(r => r.Name == "client"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("default")]);
        language.Setup(l => l.ResolveAllAsync(It.Is<RepoConnection>(r => r.Name == "api"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("default")]);

        var footprint = await Calculator(language, orchestrator: null)
            .CalculateAsync(project, "fix-bug", CancellationToken.None);

        footprint.Pods.Should().HaveCount(4, "server splits into sdk8 + sdk9, client + api one each");
        footprint.Pods.Select(p => p.Repo).Should().Equal("server", "server", "client", "api");
    }

    [Fact]
    public async Task FootprintCalculator_IncludesOrchestrator_AndSumsLimits()
    {
        var project = Project("only");
        var language = new Mock<ISandboxLanguageResolver>();
        language.Setup(l => l.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("default")]);
        var orchestrator = new ResourceLimits("100m", "500m", "128Mi", "1Gi");

        var footprint = await Calculator(language, orchestrator)
            .CalculateAsync(project, "fix-bug", CancellationToken.None);

        footprint.Pods.Should().HaveCount(2, "one sandbox + the orchestrator");
        footprint.Pods.Should().ContainSingle(p => p.Repo == "orchestrator");
        // 4Gi sandbox limit (ResourceLimits.Default) + 1Gi orchestrator = 5Gi.
        footprint.TotalMemBytes.Should().Be(5L * 1024 * 1024 * 1024);
    }

    private static RunFootprintCalculator Calculator(
        Mock<ISandboxLanguageResolver> language, ResourceLimits? orchestrator)
    {
        var resource = new Mock<ISandboxResourceResolver>();
        resource.Setup(r => r.Resolve(
                It.IsAny<ResolvedProject>(), It.IsAny<string?>(), It.IsAny<ContextYamlStackResources?>()))
            .Returns(ResourceLimits.Default); // 250m/1000m/1Gi/4Gi
        var orchestratorResolver = new Mock<IOrchestratorResourceResolver>();
        orchestratorResolver.Setup(o => o.Resolve(It.IsAny<ResolvedProject>())).Returns(orchestrator);
        return new RunFootprintCalculator(
            language.Object, resource.Object, orchestratorResolver.Object,
            NullLogger<RunFootprintCalculator>.Instance);
    }

    private static ResolvedProject Project(params string[] repos) => new()
    {
        Name = "p1",
        Repos = repos.Select(r => new RepoConnection { Name = r }).ToList(),
    };

    private static RemoteContextDiscovery Discovery(string context) =>
        new(context, ".", "csharp");
}
