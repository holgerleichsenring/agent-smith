using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// Shared builder for PipelineExecutor tests. Parametrised across the new
/// composed shape (PipelineExecutor + IPipelineStepRunner + IPipelineErrorHandler
/// + IPipelineSandboxCoordinator) and the pre-p0147e monolith
/// (PipelineExecutorLegacy). Builds both with the same mock objects so any
/// observable difference between the two surfaces as a failing assertion.
/// </summary>
public sealed class PipelineExecutorTestHarness
{
    public enum Shape { New, Legacy }

    public static IEnumerable<object[]> ExecutorShapes()
    {
        yield return new object[] { Shape.New };
        yield return new object[] { Shape.Legacy };
    }

    public Mock<ICommandExecutor> ExecutorMock { get; } = new();
    public Mock<ICommandContextFactory> FactoryMock { get; } = new();
    public Mock<ITicketProviderFactory> TicketFactoryMock { get; } = new();
    public Mock<IPipelineLifecycleCoordinator> LifecycleCoordinatorMock { get; } = new();
    public Mock<IAsyncPipelineLifecycle> LifecycleMock { get; } = new();
    public Mock<ISandboxFactory> SandboxFactoryMock { get; } = new();
    public Mock<IProgressReporter> ProgressReporterMock { get; } = new();
    public Mock<ISandboxLanguageResolver> SandboxLanguageResolverMock { get; } = new();
    public SandboxSpecBuilder SandboxSpecBuilder { get; }
    public PhaseDataFlowResolver DataFlowResolver { get; }
    public AgentSmithConfig AgentSmithConfig { get; } = new();
    public IPipelineExecutor Sut { get; }

    public PipelineExecutorTestHarness(Shape shape)
    {
        LifecycleCoordinatorMock
            .Setup(c => c.BeginAsync(It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LifecycleMock.Object);
        SandboxLanguageResolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolchainResolutionResult(null, SandboxToolchainResolutionLayer.GenericFallback));
        SandboxSpecBuilder = new SandboxSpecBuilder(new StubSandboxResourceResolver(), new StubAgentImageResolver());
        DataFlowResolver = new PhaseDataFlowResolver(Array.Empty<IPhaseDataFlow>());

        Sut = shape == Shape.Legacy
            ? BuildLegacy()
            : BuildNew();
    }

    private PipelineExecutorLegacy BuildLegacy() => new(
        ExecutorMock.Object,
        FactoryMock.Object,
        TicketFactoryMock.Object,
        LifecycleCoordinatorMock.Object,
        SandboxFactoryMock.Object,
        SandboxSpecBuilder,
        SandboxLanguageResolverMock.Object,
        ProgressReporterMock.Object,
        DataFlowResolver,
        AgentSmithConfig,
        NullLogger<PipelineExecutorLegacy>.Instance);

    private PipelineExecutor BuildNew()
    {
        var stepRunner = new PipelineStepRunner(
            ExecutorMock.Object,
            FactoryMock.Object,
            ProgressReporterMock.Object,
            DataFlowResolver,
            AgentSmithConfig,
            NullLogger<PipelineStepRunner>.Instance);

        var errorHandler = new PipelineErrorHandler(
            ExecutorMock.Object,
            FactoryMock.Object,
            TicketFactoryMock.Object,
            NullLogger<PipelineErrorHandler>.Instance);

        // SandboxCoordinator is per-run; the orchestrator pulls one from DI for
        // every ExecuteAsync. We register transient so each pipeline gets its
        // own instance and the cached sandbox stays scoped to that run.
        var services = new ServiceCollection();
        services.AddTransient<IPipelineSandboxCoordinator>(_ => new PipelineSandboxCoordinator(
            SandboxFactoryMock.Object,
            SandboxSpecBuilder,
            SandboxLanguageResolverMock.Object,
            NullLogger<PipelineSandboxCoordinator>.Instance));
        var provider = services.BuildServiceProvider();

        return new PipelineExecutor(
            provider,
            stepRunner,
            errorHandler,
            LifecycleCoordinatorMock.Object,
            NullLogger<PipelineExecutor>.Instance);
    }
}
