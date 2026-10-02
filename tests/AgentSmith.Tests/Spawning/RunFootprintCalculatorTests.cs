using Microsoft.Extensions.Options;
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

        var footprint = await Calculator(language)
            .CalculateAsync(project, "code", CancellationToken.None);

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

        var footprint = await Calculator(language)
            .CalculateAsync(project, "code", CancellationToken.None);

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
            language.Object, resource.Object, NullLogger<RunFootprintCalculator>.Instance,
            Options.Create(new SandboxGlobalConfig()));

        var footprint = await calc.CalculateAsync(project, "code", CancellationToken.None);

        footprint.Pods.Should().ContainSingle();
        footprint.Pods[0].MemLimit.Should().Be("4Gi", "the merged pod is sized to the heaviest member");
    }

    [Fact]
    public async Task RunFootprint_ContainsNoOrchestratorPod()
    {
        var project = Project("only");
        var language = new Mock<ISandboxLanguageResolver>();
        language.Setup(l => l.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("default")]);

        var footprint = await Calculator(language).CalculateAsync(project, "code", CancellationToken.None);

        footprint.Pods.Should().ContainSingle("the pipeline runs in the server; only the sandbox is a pod");
        footprint.Pods.Should().NotContain(p => p.Repo == "orchestrator");
        footprint.TotalMemBytes.Should().Be(4L * 1024 * 1024 * 1024); // the 4Gi sandbox alone
    }

    // 2026-10-01-283df: config decides the browser, so admission reserves its pod before the run starts.
    [Theory]
    [InlineData(true, "code", 2)]
    [InlineData(false, "code", 1)]
    [InlineData(true, "security-scan", 1)]
    public async Task RunFootprint_BrowserEnabled_AddsABrowserPod(bool enabled, string pipeline, int pods)
    {
        var project = Project("only");
        project = project with { Sandbox = new SandboxConfig { Browser = new ProjectBrowserConfig { Enabled = enabled } } };
        var language = new Mock<ISandboxLanguageResolver>();
        language.Setup(l => l.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Discovery("default")]);

        var footprint = await Calculator(language).CalculateAsync(project, pipeline, CancellationToken.None);

        footprint.Pods.Should().HaveCount(pods);
        if (pods == 2)
        {
            var browser = footprint.Pods[1];
            (browser.Repo, browser.Image, browser.CpuLimit, browser.MemLimit).Should().Be(
                ("browser", "browser", "2000m", "2Gi"), "the browser pod is sized at the process-wide browser profile");
            footprint.TotalMemBytes.Should().Be(6L * 1024 * 1024 * 1024);
        }
    }

    private static RunFootprintCalculator Calculator(Mock<ISandboxLanguageResolver> language)
    {
        var resource = new Mock<ISandboxResourceResolver>();
        resource.Setup(r => r.Resolve(
                It.IsAny<ResolvedProject>(), It.IsAny<string?>(), It.IsAny<ContextYamlStackResources?>()))
            .Returns(ResourceLimits.Default); // 250m/1000m/1Gi/4Gi
        return new RunFootprintCalculator(
            language.Object, resource.Object, NullLogger<RunFootprintCalculator>.Instance,
            Options.Create(new SandboxGlobalConfig()));
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
