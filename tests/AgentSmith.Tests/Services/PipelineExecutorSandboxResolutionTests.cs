using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0135: the executor's sandbox lazy-boot drives SandboxLanguageResolver
/// for sandbox-requiring pipelines and feeds the result into SandboxSpecBuilder.
/// These tests verify the end-to-end wiring: resolver-returned language →
/// matching toolchain image in the SandboxSpec the factory ultimately sees.
///
/// p0147e: lazy-boot lives in PipelineSandboxCoordinator now. Parametrised
/// across both executor shapes; same observable behaviour either way.
/// </summary>
public sealed class PipelineExecutorSandboxResolutionTests
{
    public static IEnumerable<object[]> ExecutorShapes() => PipelineExecutorTestHarness.ExecutorShapes();

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task TryCreateSandbox_ResolverReturnsCsharpFromHostCache_BuildsSpecWithDotnetImage(
        PipelineExecutorTestHarness.Shape shape)
    {
        var spec = await CaptureSpecForResolverResult(shape,
            new ToolchainResolutionResult("csharp", SandboxToolchainResolutionLayer.HostCache));

        spec.ToolchainImage.Should().Be("mcr.microsoft.com/dotnet/sdk:8.0");
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task TryCreateSandbox_ResolverReturnsTypeScriptFromRemoteContextYaml_BuildsSpecWithNodeImage(
        PipelineExecutorTestHarness.Shape shape)
    {
        var spec = await CaptureSpecForResolverResult(shape,
            new ToolchainResolutionResult("TypeScript", SandboxToolchainResolutionLayer.RemoteContextYaml));

        spec.ToolchainImage.Should().Be("node:20-bookworm-slim");
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task TryCreateSandbox_ResolverReturnsNullFromGenericFallback_BuildsSpecWithGenericImage(
        PipelineExecutorTestHarness.Shape shape)
    {
        var spec = await CaptureSpecForResolverResult(shape,
            new ToolchainResolutionResult(null, SandboxToolchainResolutionLayer.GenericFallback));

        spec.ToolchainImage.Should().Be("buildpack-deps:bookworm-scm");
    }

    private static async Task<SandboxSpec> CaptureSpecForResolverResult(
        PipelineExecutorTestHarness.Shape shape, ToolchainResolutionResult resolverResult)
    {
        var h = new PipelineExecutorTestHarness(shape);
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

        var sut = new PipelineExecutor(
            _executorMock.Object,
            _factoryMock.Object,
            _ticketFactoryMock.Object,
            _coordinatorMock.Object,
            _sandboxFactoryMock.Object,
            new SandboxSpecBuilder(new StubSandboxResourceResolver(), new StubAgentImageResolver()),
            _resolverMock.Object,
            _progressReporterMock.Object,
            new AgentSmith.Application.Services.Pipeline.PhaseDataFlowResolver(
                Array.Empty<AgentSmith.Contracts.Pipeline.IPhaseDataFlow>()),
            new AgentSmithConfig(),
            new AgentSmith.Application.Services.SkillRounds.SkillRoundBufferDispatcher(),
            NullLogger<PipelineExecutor>.Instance);

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
