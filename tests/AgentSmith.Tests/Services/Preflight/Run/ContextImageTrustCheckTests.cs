using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// 2026-10-12-6bfac: a context image the registry boundary refuses is a finding ON the run —
/// naming the context, the image, the boundary and the lever — not a server-log line while
/// the run spends half an hour in the image it got instead.
/// </summary>
public sealed class ContextImageTrustCheckTests
{
    private const string Refused = "sbtscala/scala-sbt:eclipse-temurin-25_1.11.7_2.13.18";
    private const string SandboxKey = "app";

    [Fact]
    public async Task ContextImageTrustCheck_RefusedImage_WarnsWithLever()
    {
        var finding = await RunAsync(Refused, new ResolvedProject());

        finding.Verdict.Should().Be(RunPreflightVerdict.Warn,
            "the language catalog may still carry the toolchain — probably wrong is a report");
        finding.Message.Should().Contain("context 'backend'").And.Contain(Refused)
            .And.Contain("mcr.microsoft.com/");
        finding.Lever.Should().Contain("'sbtscala/'").And.Contain("sandbox.allowed_registries")
            .And.Contain("sandbox.images");
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
    public async Task ContextImageTrustCheck_HostBackendRecordsNoImage_Passes() =>
        (await RunAsync(Refused, new ResolvedProject(), recordImage: false))
            .Verdict.Should().Be(RunPreflightVerdict.Pass,
                "a sandbox that runs on the host runs no image, so nothing was refused");

    private static Task<RunPreflightFinding> RunAsync(
        string contextImage, ResolvedProject project, bool recordImage = true)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>
            {
                [SandboxKey] = [new RemoteContextDiscovery("backend", ".", "scala", ToolchainImage: contextImage)],
            });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxImages,
            recordImage
                ? new Dictionary<string, string> { [SandboxKey] = "buildpack-deps:bookworm-scm" }
                : new Dictionary<string, string>());
        pipeline.Set(ContextKeys.ProjectConfig, project);
        return new ContextImageTrustCheck(ContextGates.ImageBoundary())
            .RunAsync(pipeline, CancellationToken.None);
    }
}
