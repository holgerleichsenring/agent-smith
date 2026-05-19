using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// The executor's sandbox lazy-boot drives SandboxLanguageResolver for
/// sandbox-requiring pipelines and feeds the result into SandboxSpecBuilder.
/// These tests verify the end-to-end wiring: resolver-returned language →
/// matching toolchain image in the SandboxSpec the factory ultimately sees.
/// Lazy-boot lives in PipelineSandboxCoordinator.
/// </summary>
public sealed class PipelineExecutorSandboxResolutionTests
{
    [Fact]
    public async Task TryCreateSandbox_ResolverReturnsCsharpFromHostCache_BuildsSpecWithDotnetImage()
    {
        var spec = await CaptureSpecForResolverResult(
            new ToolchainResolutionResult("csharp", SandboxToolchainResolutionLayer.HostCache));

        spec.ToolchainImage.Should().Be("mcr.microsoft.com/dotnet/sdk:8.0");
    }

    [Fact]
    public async Task TryCreateSandbox_ResolverReturnsTypeScriptFromRemoteContextYaml_BuildsSpecWithNodeImage()
    {
        var spec = await CaptureSpecForResolverResult(
            new ToolchainResolutionResult("TypeScript", SandboxToolchainResolutionLayer.RemoteContextYaml));

        spec.ToolchainImage.Should().Be("node:20-bookworm-slim");
    }

    [Fact]
    public async Task TryCreateSandbox_ResolverReturnsNullFromGenericFallback_BuildsSpecWithGenericImage()
    {
        var spec = await CaptureSpecForResolverResult(
            new ToolchainResolutionResult(null, SandboxToolchainResolutionLayer.GenericFallback));

        spec.ToolchainImage.Should().Be("buildpack-deps:bookworm-scm");
    }

    private static async Task<SandboxSpec> CaptureSpecForResolverResult(ToolchainResolutionResult resolverResult)
    {
        var h = new PipelineExecutorTestBuilder();
        h.SandboxLanguageResolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolverResult);

        // Capture-and-throw so the spec is observed before the pipeline tears down.
        // Throwing is the cheapest way to short-circuit ExecuteAsync without setting up
        // a real ICommandContext for CheckoutSource (the alternative was wiring up the
        // full factory/executor mock chain, which buys nothing for this assertion).
        SandboxSpec? captured = null;
        h.SandboxFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .Callback<SandboxSpec, CancellationToken>((s, _) => captured = s)
            .ThrowsAsync(new InvalidOperationException("test-only — short-circuit after spec capture"));

        var commands = new[] { CommandNames.CheckoutSource };
        var repoConnection = new RepoConnection();
        var project = new ResolvedProject { Repos = new[] { repoConnection } };
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentRepo, repoConnection);
        var act = async () => await h.Sut.ExecuteAsync(
            commands, project, pipeline, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        captured.Should().NotBeNull("spec should be captured before the throw");
        return captured!;
    }
}
