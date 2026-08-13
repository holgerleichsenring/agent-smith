using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Sandbox;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// Composes the live <see cref="PipelineExecutor"/> with mocked collaborators
/// for the PipelineExecutor*Tests files. Exposes mocks so each test can stage
/// the specific call it cares about and inspect verifies after the run.
/// </summary>
internal sealed class PipelineExecutorTestBuilder
{
    public Mock<ICommandExecutor> ExecutorMock { get; } = new();
    public Mock<ICommandContextFactory> FactoryMock { get; } = new();
    public Mock<ITicketProviderFactory> TicketFactoryMock { get; } = new();
    public Mock<IPipelineLifecycleCoordinator> LifecycleCoordinatorMock { get; } = new();
    public Mock<IRunCancellationRegistry> CancellationRegistryMock { get; } = new();
    public Mock<IAsyncPipelineLifecycle> LifecycleMock { get; } = new();
    public Mock<ISandboxFactory> SandboxFactoryMock { get; } = new();
    public Mock<IProgressReporter> ProgressReporterMock { get; } = new();
    public Mock<ISandboxLanguageResolver> SandboxLanguageResolverMock { get; } = new();
    public SandboxSpecBuilder SandboxSpecBuilder { get; }
    public PhaseDataFlowResolver DataFlowResolver { get; } = new(Array.Empty<IPhaseDataFlow>());
    public AgentSmithConfig AgentSmithConfig { get; } = new();
    public IPipelineExecutor Sut { get; }

    /// <param name="eventPublisher">
    /// Defaults to the no-op publisher. Tests that assert on the step-event stream
    /// (p0312d: one StepStarted/StepFinished pair per command) pass a recording one.
    /// </param>
    /// <param name="sandboxCoordinator">
    /// Defaults to the real coordinator over mocked infrastructure. A test that walks
    /// whole presets substitutes a stub: provisioning is not what it is asserting, and
    /// the real coordinator needs a fully staged repo inventory to get that far.
    /// </param>
    public PipelineExecutorTestBuilder(
        IEventPublisher? eventPublisher = null,
        IPipelineSandboxCoordinator? sandboxCoordinator = null)
    {
        var events = eventPublisher ?? EventTestStubs.NoOp;
        LifecycleCoordinatorMock
            .Setup(c => c.BeginAsync(It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LifecycleMock.Object);
        SandboxLanguageResolverMock
            .Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RemoteContextDiscovery("default", ".", null) });
        SandboxSpecBuilder = new SandboxSpecBuilder(new StubSandboxResourceResolver(), new StubAgentImageResolver());

        var dataFlowReadGate = new DataFlowReadGate(
            DataFlowResolver,
            Microsoft.Extensions.Options.Options.Create(AgentSmithConfig.PipelineDataFlow),
            NullLogger<DataFlowReadGate>.Instance);
        var stepRunner = new PipelineStepRunner(
            ExecutorMock.Object,
            FactoryMock.Object,
            ProgressReporterMock.Object,
            dataFlowReadGate,
            events,
            EventTestStubs.RunContext,
            NullLogger<PipelineStepRunner>.Instance);

        var errorHandler = new PipelineErrorHandler(
            ExecutorMock.Object,
            FactoryMock.Object,
            TicketFactoryMock.Object,
            NullLogger<PipelineErrorHandler>.Instance);

        // SandboxCoordinator owns mutable per-run state; the executor resolves a
        // fresh transient instance per ExecuteAsync.
        var services = new ServiceCollection();
        services.AddTransient<IPipelineSandboxCoordinator>(_ => sandboxCoordinator ?? new PipelineSandboxCoordinator(
            SandboxFactoryMock.Object,
            SandboxSpecBuilder,
            SandboxLanguageResolverMock.Object,
            EventTestStubs.NoOp,
            EventTestStubs.RunContext,
            new NoOpSandboxLivenessSupervisor(),
            NullLogger<PipelineSandboxCoordinator>.Instance));
        var provider = services.BuildServiceProvider();

        Sut = new PipelineExecutor(
            provider,
            stepRunner,
            errorHandler,
            LifecycleCoordinatorMock.Object,
            CancellationRegistryMock.Object,
            new PipelineExecutorPolicy(NullLogger<PipelineExecutorPolicy>.Instance),
            new PipelineFinalizerTail(stepRunner, NullLogger<PipelineFinalizerTail>.Instance),
            new PlannedStepsAnnouncer(EventTestStubs.NoOp),
            NullLogger<PipelineExecutor>.Instance);
    }
}
