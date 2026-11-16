using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// 2026-10-12-6bfaa: a sandbox on the generic fallback image is said on the run — which sandbox,
/// why, and the levers — instead of in a server log the run never shows.
/// </summary>
public sealed class ToolchainImageFallbackCheckTests
{
    private const string Fallback = SandboxImageChain.GenericFallbackImage;
    private const string Searched =
        "no context.yaml under .agentsmith/contexts/ (the default branch; branch 'agent-smith/4711' is not on the remote)";

    [Fact]
    public async Task FallbackImage_WarnsWithReasonAndLever()
    {
        var pipeline = Pipeline(Fallback, new RemoteContextDiscovery("default", ".", null, Provenance: Searched));

        var finding = await Check().RunAsync(pipeline, CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Warn);
        finding.Message.Should().Contain("repo 'app'").And.Contain(Fallback).And.Contain(Searched);
        finding.Lever.Should().Contain("merge the branch").And.Contain("sandbox.images.<lang>");
        finding.Describe().Should().Contain("toolchain-image:");
    }

    [Fact]
    public async Task UnknownLanguage_NamesTheLanguage()
    {
        var pipeline = Pipeline(Fallback, new RemoteContextDiscovery("default", ".", "cobol", Provenance: "read on x"));

        var finding = await Check().RunAsync(pipeline, CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Warn);
        finding.Message.Should().Contain("lang=cobol");
    }

    [Fact]
    public async Task OperatorNamedFallback_Passes()
    {
        var pipeline = Pipeline(Fallback, new RemoteContextDiscovery("default", ".", null),
            new SandboxConfig { ToolchainImage = Fallback });

        var finding = await Check().RunAsync(pipeline, CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public async Task ChosenImage_OrNoRecordedImage_Passes()
    {
        var chosen = Pipeline("mcr.microsoft.com/dotnet/sdk:10.0", new RemoteContextDiscovery("default", ".", "dotnet"));

        (await Check().RunAsync(chosen, CancellationToken.None))
            .Verdict.Should().Be(RunPreflightVerdict.Pass);
        (await Check().RunAsync(new PipelineContext(), CancellationToken.None))
            .Verdict.Should().Be(RunPreflightVerdict.Pass, "a run without repositories has no context to judge");
    }

    [Fact]
    public async Task ToolchainImageFallbackCheck_FellThroughWithVerifyStages_Fails()
    {
        var pipeline = Pipeline(Fallback, new RemoteContextDiscovery("backend", ".", "cobol",
            Verify: [new ContextYamlVerifyStage("build", "cobc -x main.cbl")], Provenance: "read on x"));

        var finding = await Check().RunAsync(pipeline, CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail,
            "a declared build stage cannot run on an image with no toolchain");
        finding.Message.Should().Contain("repo 'app'").And.Contain("lang=cobol").And.Contain("cobc -x main.cbl");
        finding.Lever.Should().Contain("sandbox.images.<lang>").And.Contain("Sandbox tab");
    }

    [Fact]
    public async Task ToolchainImageFallbackCheck_FellThroughWithoutStages_Warns() =>
        (await Check().RunAsync(
            Pipeline(Fallback, new RemoteContextDiscovery("docs", ".", null)), CancellationToken.None))
            .Verdict.Should().Be(RunPreflightVerdict.Warn, "a sandbox that declares nothing to build may run there");

    [Fact]
    public async Task ToolchainImageFallbackCheck_RefusedImageOnGeneric_LeavesItToTheTrustCheck()
    {
        var pipeline = Pipeline(Fallback, new RemoteContextDiscovery("backend", ".", "scala",
            ToolchainImage: "sbtscala/scala-sbt:1", Verify: [new ContextYamlVerifyStage("build", "sbt compile")]));

        (await Check().RunAsync(pipeline, CancellationToken.None)).Verdict.Should().Be(RunPreflightVerdict.Pass,
            "the context named an image; context-image-trust reports its refusal, once");
    }

    [Fact]
    public async Task ToolchainImageFallbackCheck_OperatorNamedGenericForTheLanguage_Passes()
    {
        var pipeline = Pipeline(Fallback, new RemoteContextDiscovery("backend", ".", "cobol",
                Verify: [new ContextYamlVerifyStage("build", "cobc -x main.cbl")]),
            new SandboxConfig { Images = new Dictionary<string, string> { ["cobol"] = Fallback } });

        (await Check().RunAsync(pipeline, CancellationToken.None)).Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    private static ToolchainImageFallbackCheck Check(bool containers = true) =>
        new(new SandboxImageChain(), PreflightInventories.Over(), new SandboxContainerRuntime(containers));

    // 2026-11-16-f3fba: the image is no longer read off a sandbox; the chain decides it from the context.
    private static PipelineContext Pipeline(string image, RemoteContextDiscovery discovery, SandboxConfig? sandbox = null)
    {
        _ = image;
        var pipeline = new PipelineContext();
        PreflightInventories.Seed(pipeline, "app", discovery);
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p", Sandbox = sandbox });
        return pipeline;
    }

    [Fact]
    public async Task ToolchainImageFallbackCheck_HostBackend_Passes()
    {
        var pipeline = Pipeline(Fallback, new RemoteContextDiscovery("backend", ".", "cobol",
            Verify: [new ContextYamlVerifyStage("build", "cobc -x main.cbl")]));

        (await Check(containers: false).RunAsync(pipeline, CancellationToken.None))
            .Verdict.Should().Be(RunPreflightVerdict.Pass, "the in-process backend runs the host's toolchain");
    }

    [Fact]
    public async Task ToolchainImageFallbackCheck_FellThroughWithVerifyStages_CarriesTheKeyToSet()
    {
        var pipeline = Pipeline(Fallback, new RemoteContextDiscovery("backend", ".", "cobol",
            Verify: [new ContextYamlVerifyStage("build", "cobc -x main.cbl")]));

        var detail = (await Check().RunAsync(pipeline, CancellationToken.None)).Detail;

        detail!.Subject.Should().Be("Context backend (lang cobol)");
        detail.Repos.Should().Equal("app");
        detail.Control.Should().Be("sandbox.images.cobol");
    }

    [Fact]
    public async Task ToolchainImageFallbackCheck_InventoryAbsent_BuildsIt()
    {
        var resolver = new Mock<ISandboxLanguageResolver>();
        resolver.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<AgentSmith.Application.Models.ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RemoteContextDiscovery("docs", ".", null)]);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "app" }]);
        var check = new ToolchainImageFallbackCheck(new SandboxImageChain(), PreflightInventories.Over(resolver.Object),
            PreflightInventories.Containers);

        var finding = await check.RunAsync(pipeline, CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Warn, "pr-review and security-scan carry no inventory; it is read here");
        finding.Message.Should().Contain("repo 'app'");
    }
}
