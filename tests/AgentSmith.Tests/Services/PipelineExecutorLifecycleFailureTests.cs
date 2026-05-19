using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// Regression: production runs that threw during sandbox setup were leaving the
/// ticket transitioned to Done instead of Failed because lifecycle.MarkFailed
/// only fired on result.IsSuccess=false — exception paths bypassed it.
///
/// Parametrised across the new composed executor and the pre-p0147e monolith.
/// </summary>
public sealed class PipelineExecutorLifecycleFailureTests
{
    public static IEnumerable<object[]> ExecutorShapes() => PipelineExecutorTestHarness.ExecutorShapes();

    public PipelineExecutorLifecycleFailureTests()
    {
        _coordinatorMock
            .Setup(c => c.BeginAsync(It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_lifecycleMock.Object);
        var resolverMock = new Mock<ISandboxLanguageResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolchainResolutionResult(null, SandboxToolchainResolutionLayer.GenericFallback));
        _sut = new PipelineExecutor(
            _executorMock.Object,
            _factoryMock.Object,
            _ticketFactoryMock.Object,
            _coordinatorMock.Object,
            _sandboxFactoryMock.Object,
            new SandboxSpecBuilder(new StubSandboxResourceResolver(), new StubAgentImageResolver()),
            resolverMock.Object,
            _progressReporterMock.Object,
            new AgentSmith.Application.Services.Pipeline.PhaseDataFlowResolver(
                Array.Empty<AgentSmith.Contracts.Pipeline.IPhaseDataFlow>()),
            new AgentSmithConfig(),
            new AgentSmith.Application.Services.SkillRounds.SkillRoundBufferDispatcher(),
            NullLogger<PipelineExecutor>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_SandboxFactoryThrows_MarksLifecycleFailed()
    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_SandboxFactoryThrows_MarksLifecycleFailed(
        PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        // Arrange — sandbox factory throws (production symptom: K8s Forbidden because
        // the pod's ServiceAccount lacked pods/create RBAC).
        h.SandboxFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("K8s pod create denied"));

        // The pipeline must contain a sandbox-requiring command for sandbox-coordinator
        // to actually invoke the factory; CheckoutSourceCommand is the canonical example.
        var commands = new[] { CommandNames.CheckoutSource };
        // p0140d: executor reads CurrentRepo from the pipeline context for
        // sandbox-language resolution; tests must seed it explicitly.
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentRepo, new RepoConnection());

        // Act
        var act = async () => await h.Sut.ExecuteAsync(
            commands, new ResolvedProject(), pipeline, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        h.LifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_AllCommandsSucceed_DoesNotMarkLifecycleFailed(
        PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        // Regression guard for the happy path.
        var result = await h.Sut.ExecuteAsync(
            Array.Empty<string>(), new ResolvedProject(), new PipelineContext(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        h.LifecycleMock.Verify(l => l.MarkFailed(), Times.Never);
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_CommandReturnsFail_MarksLifecycleFailed(
        PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        // Regression guard for the previously-working failure path.
        var commands = new[] { "BadCommand" };
        h.FactoryMock
            .Setup(f => f.Create(PipelineCommand.Simple("BadCommand"), It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>()))
            .Throws(new Exception("Command-level failure"));

        var result = await h.Sut.ExecuteAsync(
            commands, new ResolvedProject(), new PipelineContext(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        h.LifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }
}
