using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

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
        finding.Message.Should().Contain("sandbox 'app'").And.Contain(Fallback).And.Contain(Searched);
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
        var chosen = Pipeline("sbtscala/scala-sbt:17", new RemoteContextDiscovery("default", ".", "scala"));

        (await Check().RunAsync(chosen, CancellationToken.None))
            .Verdict.Should().Be(RunPreflightVerdict.Pass);
        (await Check().RunAsync(new PipelineContext(), CancellationToken.None))
            .Verdict.Should().Be(RunPreflightVerdict.Pass, "the in-process backend records no image");
    }

    [Fact]
    public async Task ToolchainImageFallbackCheck_FellThroughWithVerifyStages_Fails()
    {
        var pipeline = Pipeline(Fallback, new RemoteContextDiscovery("backend", ".", "cobol",
            Verify: [new ContextYamlVerifyStage("build", "cobc -x main.cbl")], Provenance: "read on x"));

        var finding = await Check().RunAsync(pipeline, CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail,
            "a declared build stage cannot run on an image with no toolchain");
        finding.Message.Should().Contain("sandbox 'app'").And.Contain("lang=cobol").And.Contain("cobc -x main.cbl");
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

    private static ToolchainImageFallbackCheck Check() => new(new SandboxImageChain());

    private static PipelineContext Pipeline(string image, RemoteContextDiscovery discovery, SandboxConfig? sandbox = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxImages, new Dictionary<string, string> { ["app"] = image });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, new Dictionary<string, RemoteContextDiscovery> { ["app"] = discovery });
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p", Sandbox = sandbox });
        return pipeline;
    }
}
