using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0135: PipelineExecutor.TryCreateSandboxAsync drives SandboxLanguageResolver
/// for sandbox-requiring pipelines and feeds the result into SandboxSpecBuilder.
/// These tests verify the end-to-end wiring: resolver-returned language →
/// matching toolchain image in the SandboxSpec the factory ultimately sees.
/// </summary>
public sealed class PipelineExecutorSandboxResolutionTests
{
    private readonly Mock<ICommandExecutor> _executorMock = new();
    private readonly Mock<ICommandContextFactory> _factoryMock = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
    private readonly Mock<IPipelineLifecycleCoordinator> _coordinatorMock = new();
    private readonly Mock<IAsyncPipelineLifecycle> _lifecycleMock = new();
    private readonly Mock<ISandboxFactory> _sandboxFactoryMock = new();
    private readonly Mock<ISandboxLanguageResolver> _resolverMock = new();
    private readonly Mock<IProgressReporter> _progressReporterMock = new();

    public PipelineExecutorSandboxResolutionTests()
    {
        _coordinatorMock
            .Setup(c => c.BeginAsync(It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_lifecycleMock.Object);
    }

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

    private async Task<SandboxSpec> CaptureSpecForResolverResult(ToolchainResolutionResult resolverResult)
    {
        _resolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolverResult);

        // Capture-and-throw so the spec is observed before the pipeline tears down.
        // Throwing is the cheapest way to short-circuit ExecuteAsync without setting up
        // a real ICommandContext for CheckoutSource (the alternative was wiring up the
        // full factory/executor mock chain, which buys nothing for this assertion).
        SandboxSpec? captured = null;
        _sandboxFactoryMock
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
        var act = async () => await sut.ExecuteAsync(
            commands, project, pipeline, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        captured.Should().NotBeNull("spec should be captured before the throw");
        return captured!;
    }
}
