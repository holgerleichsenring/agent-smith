using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// Regression: production runs that threw during sandbox setup were leaving the
/// ticket transitioned to Done instead of Failed because lifecycle.MarkFailed
/// only fired on result.IsSuccess=false — exception paths bypassed it.
/// </summary>
public sealed class PipelineExecutorLifecycleFailureTests
{
    private readonly Mock<ICommandExecutor> _executorMock = new();
    private readonly Mock<ICommandContextFactory> _factoryMock = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
    private readonly Mock<IPipelineLifecycleCoordinator> _coordinatorMock = new();
    private readonly Mock<IAsyncPipelineLifecycle> _lifecycleMock = new();
    private readonly Mock<ISandboxFactory> _sandboxFactoryMock = new();
    private readonly Mock<IProgressReporter> _progressReporterMock = new();
    private readonly PipelineExecutor _sut;

    public PipelineExecutorLifecycleFailureTests()
    {
        _coordinatorMock
            .Setup(c => c.BeginAsync(It.IsAny<ProjectConfig>(), It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_lifecycleMock.Object);
        _sut = new PipelineExecutor(
            _executorMock.Object,
            _factoryMock.Object,
            _ticketFactoryMock.Object,
            _coordinatorMock.Object,
            _sandboxFactoryMock.Object,
            new SandboxSpecBuilder(),
            _progressReporterMock.Object,
            NullLogger<PipelineExecutor>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_SandboxFactoryThrows_MarksLifecycleFailed()
    {
        // Arrange — sandbox factory throws (production symptom: K8s Forbidden because
        // the pod's ServiceAccount lacked pods/create RBAC).
        _sandboxFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("K8s pod create denied"));

        // The pipeline must contain a sandbox-requiring command for TryCreateSandboxAsync
        // to actually invoke the factory; CheckoutSourceCommand is the canonical example.
        var commands = new[] { CommandNames.CheckoutSource };

        // Act
        var act = async () => await _sut.ExecuteAsync(
            commands, new ProjectConfig(), new PipelineContext(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AllCommandsSucceed_DoesNotMarkLifecycleFailed()
    {
        // Regression guard for the happy path.
        var result = await _sut.ExecuteAsync(
            Array.Empty<string>(), new ProjectConfig(), new PipelineContext(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CommandReturnsFail_MarksLifecycleFailed()
    {
        // Regression guard for the previously-working failure path.
        var commands = new[] { "BadCommand" };
        _factoryMock
            .Setup(f => f.Create(PipelineCommand.Simple("BadCommand"), It.IsAny<ProjectConfig>(), It.IsAny<PipelineContext>()))
            .Throws(new Exception("Command-level failure"));

        var result = await _sut.ExecuteAsync(
            commands, new ProjectConfig(), new PipelineContext(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }
}
