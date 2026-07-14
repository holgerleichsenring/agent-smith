using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// p0336 + p0336c: the footprint sizes ONE pod per (repo, toolchain image) — the
/// same grouping the coordinator spawns. Distinct images within a repo split
/// (sdk8 vs sdk9); same-image contexts collapse into one pod at the max resource
/// envelope, so the reserved footprint equals the pods that actually run.
/// </summary>
public sealed class RunFootprintCalculatorTests
{
    [Fact]
    public async Task FootprintCalculator_DistinctImagesInRepo_SplitPerImage()
    {
        var project = Project("server", "client", "api");
        var language = new Mock<ISandboxLanguageResolver>();
        language.Setup(l => l.ResolveAllAsync(It.Is<RepoConnection>(r => r.Name == "server"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("sdk8", image: "dotnet:8"), Discovery("sdk9", image: "dotnet:9")]);
        language.Setup(l => l.ResolveAllAsync(It.Is<RepoConnection>(r => r.Name == "client"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("default")]);
        language.Setup(l => l.ResolveAllAsync(It.Is<RepoConnection>(r => r.Name == "api"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("default")]);

        var footprint = await Calculator(language, orchestrator: null)
            .CalculateAsync(project, "fix-bug", CancellationToken.None);

        footprint.Pods.Should().HaveCount(4, "server splits sdk8 + sdk9 (distinct images); client + api one each");
        footprint.Pods.Select(p => p.Repo).Should().Equal("server", "server", "client", "api");
    }

    // p0336c: the DAP-Server case after the encrypter net9->net8 migration — five
    // same-image contexts of one repo are ONE pod, not five (they build
    // sequentially under the one agentic loop).
    [Fact]
    public async Task FootprintCalculator_SameImageContexts_CollapseToOnePod()
    {
        var project = Project("server");
        var language = new Mock<ISandboxLanguageResolver>();
        language.Setup(l => l.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("api"), Discovery("encrypter"), Discovery("test-data-generator"),
                Discovery("client-api-generator"), Discovery("okta")]);

        var footprint = await Calculator(language, orchestrator: null)
            .CalculateAsync(project, "fix-bug", CancellationToken.None);

        footprint.Pods.Should().ContainSingle("all five contexts share one toolchain image");
        footprint.Pods[0].Contexts.Should().HaveCount(5);
    }

    [Fact]
    public async Task FootprintCalculator_MergedGroup_UsesMaxResourceEnvelope()
    {
        var project = Project("server");
        var language = new Mock<ISandboxLanguageResolver>();
        language.Setup(l => l.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("light", memLimit: "3Gi"), Discovery("heavy", memLimit: "4Gi")]);
        // Resolver echoes each context's declared memory limit.
        var resource = new Mock<ISandboxResourceResolver>();
        resource.Setup(r => r.Resolve(It.IsAny<ResolvedProject>(), It.IsAny<string?>(), It.IsAny<ContextYamlStackResources?>()))
            .Returns<ResolvedProject, string?, ContextYamlStackResources?>(
                (_, _, res) => new ResourceLimits("250m", "1", "1Gi", res?.MemoryLimit ?? "1Gi"));
        var calc = new RunFootprintCalculator(
            language.Object, resource.Object, NoOrchestrator(), NullLogger<RunFootprintCalculator>.Instance);

        var footprint = await calc.CalculateAsync(project, "fix-bug", CancellationToken.None);

        footprint.Pods.Should().ContainSingle();
        footprint.Pods[0].MemLimit.Should().Be("4Gi", "the merged pod is sized to the heaviest member");
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
        footprint.TotalMemBytes.Should().Be(5L * 1024 * 1024 * 1024); // 4Gi sandbox + 1Gi orchestrator
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

    private static IOrchestratorResourceResolver NoOrchestrator()
    {
        var m = new Mock<IOrchestratorResourceResolver>();
        m.Setup(o => o.Resolve(It.IsAny<ResolvedProject>())).Returns((ResourceLimits?)null);
        return m.Object;
    }

    private static ResolvedProject Project(params string[] repos) => new()
    {
        Name = "p1",
        Repos = repos.Select(r => new RepoConnection { Name = r }).ToList(),
    };

    private static RemoteContextDiscovery Discovery(string context, string? image = null, string? memLimit = null) =>
        new(context, ".", "csharp", ToolchainImage: image,
            Resources: memLimit is null ? null : new ContextYamlStackResources { MemoryLimit = memLimit });
}
