using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// 2026-10-12-6bfac: a context image the registry boundary refuses is a finding ON the run —
/// naming the context, the image, the boundary and the lever — not a server-log line while
/// the run spends half an hour in the image it got instead.
/// </summary>
public sealed class ContextImageTrustCheckTests
{
    private const string Refused = "sbtscala/scala-sbt:eclipse-temurin-25_1.11.7_2.13.18";
    private const string Repo = "app";

    [Fact]
    public async Task ContextImageTrustCheck_RefusedImage_FailsWithLever()
    {
        var finding = await RunAsync(Refused, new ResolvedProject());

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail,
            "2026-10-12-50bda: the run does not work in an image substituted for the declared one");
        finding.Message.Should().Contain("context 'backend'").And.Contain(Refused)
            .And.Contain("mcr.microsoft.com/");
        finding.Message.Should().Contain("will not substitute another");
        finding.Lever.Should().Contain("'sbtscala/'").And.Contain("sandbox.allowed_registries")
            .And.Contain("Trusted image registries").And.Contain("Sandbox tab").And.Contain("sandbox.images")
            .And.Contain("declare a stack.image inside the boundary");
        finding.Describe().Should().Contain("context-image-trust");
    }

    [Fact]
    public async Task ContextImageTrustCheck_OperatorImageConfigured_Passes()
    {
        var perLanguage = new ResolvedProject
        {
            Sandbox = new SandboxConfig { Images = new() { ["scala"] = "ghcr.io/team/sbt:1" } },
        };
        var wholeProject = new ResolvedProject
        {
            Sandbox = new SandboxConfig { ToolchainImage = "ghcr.io/team/sbt:1" },
        };

        (await RunAsync(Refused, perLanguage)).Verdict.Should().Be(RunPreflightVerdict.Pass,
            "the chain never consults a context image the operator already overrode");
        (await RunAsync(Refused, wholeProject)).Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public async Task ContextImageTrustCheck_TrustedImage_Passes() =>
        (await RunAsync("mcr.microsoft.com/dotnet/sdk:8.0", new ResolvedProject()))
            .Verdict.Should().Be(RunPreflightVerdict.Pass);

    [Fact]
    public async Task ContextImageTrustCheck_HostBackend_Passes() =>
        (await RunAsync(Refused, new ResolvedProject(), containers: false))
            .Verdict.Should().Be(RunPreflightVerdict.Pass,
                "a backend that runs on the host runs no image, so nothing was refused");

    [Fact]
    public async Task ContextImageTrustCheck_RefusedImage_CarriesKindAndDetail()
    {
        var finding = await RunAsync(Refused, new ResolvedProject());

        finding.Kind.Should().Be(RunEndKinds.ImageUntrusted);
        finding.Detail!.Subject.Should().Be($"Image {Refused} (context backend)");
        finding.Detail.Repos.Should().Equal(Repo);
        finding.Detail.Control.Should().Be("Trusted image registries");
        finding.Detail.SetTo.Should().Be("mcr.microsoft.com/, ghcr.io/, sbtscala/",
            "naming one registry replaces the defaults, so the value keeps them");
        finding.Detail.SettingsPath.Should().Be("/config/settings/sandbox");
    }

    [Fact]
    public async Task ContextImageTrustCheck_RefusedLibraryImage_SetsTheLibraryToTrust()
    {
        var trust = new Mock<IImageRegistryTrust>();
        trust.Setup(t => t.InForce()).Returns(RegistryTrustBoundary.From(
            new SandboxGlobalConfig { AllowedRegistries = ["ghcr.io/"] }));

        var detail = (await RunAsync("node:22-bookworm", new ResolvedProject(), trust: trust.Object, boundaryTrust: trust.Object)).Detail;

        detail!.Subject.Should().Be("Image node:22-bookworm (context backend)");
        detail.Control.Should().Be("Docker Hub official library images");
        detail.SetTo.Should().Be("Trust");
    }

    [Fact]
    public async Task ContextImageTrustCheck_ScopedContexts_JudgesOnlyTheKeptOnes()
    {
        var pipeline = new PipelineContext();
        PreflightInventories.Seed(pipeline, Repo,
            new RemoteContextDiscovery("backend", ".", "scala", ToolchainImage: Refused),
            new RemoteContextDiscovery("web", "web", "node"));
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<string>>>(ContextKeys.ScopedContexts,
            new Dictionary<string, IReadOnlyList<string>> { [Repo] = ["web"] });

        var finding = await Check().RunAsync(pipeline, CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass, "the scoped-out context gets no sandbox");
    }

    [Fact]
    public async Task ContextImageTrustCheck_RepoScopedOut_IsNotJudged()
    {
        var pipeline = new PipelineContext();
        PreflightInventories.Seed(pipeline, Repo, new RemoteContextDiscovery("backend", ".", "scala", ToolchainImage: Refused));
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "other" }]);

        (await Check().RunAsync(pipeline, CancellationToken.None)).Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public async Task ContextImageTrustCheck_RefusedImageTheChainRunsAnyway_Passes()
    {
        // A boundary naming one registry stops trusting Docker Hub library images, so a context
        // that declared the generic image on purpose is refused — and lands on that very image.
        var trust = new Mock<IImageRegistryTrust>();
        trust.Setup(t => t.InForce()).Returns(RegistryTrustBoundary.From(
            new SandboxGlobalConfig { AllowedRegistries = ["ghcr.io/"] }));

        (await RunAsync(SandboxImageChain.GenericFallbackImage, new ResolvedProject(), trust: trust.Object))
            .Verdict.Should().Be(RunPreflightVerdict.Pass, "the sandbox runs exactly what was declared");
    }

    private static Task<RunPreflightFinding> RunAsync(
        string contextImage, ResolvedProject project, bool containers = true,
        IImageRegistryTrust? trust = null, IImageRegistryTrust? boundaryTrust = null)
    {
        var pipeline = new PipelineContext();
        PreflightInventories.Seed(pipeline, Repo,
            new RemoteContextDiscovery("backend", ".", "scala", ToolchainImage: contextImage));
        pipeline.Set(ContextKeys.ProjectConfig, project);
        return Check(trust, containers, boundaryTrust).RunAsync(pipeline, CancellationToken.None);
    }

    private static ContextImageTrustCheck Check(
        IImageRegistryTrust? trust = null, bool containers = true, IImageRegistryTrust? boundaryTrust = null) =>
        new(boundaryTrust is null ? ContextGates.ImageBoundary() : new ContextImageBoundary(boundaryTrust), new SandboxImageChain(trust),
            PreflightInventories.Over(), new SandboxContainerRuntime(containers));
}
