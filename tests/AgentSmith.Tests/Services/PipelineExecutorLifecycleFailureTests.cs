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
/// </summary>
public sealed class PipelineExecutorLifecycleFailureTests
{
    [Fact]
    public async Task ExecuteAsync_SandboxFactoryThrows_MarksLifecycleFailed()
    {
        var h = new PipelineExecutorTestBuilder();
        // Sandbox factory throws (production symptom: K8s Forbidden because the pod's
        // ServiceAccount lacked pods/create RBAC).
        h.SandboxFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("K8s pod create denied"));

        // Pipeline must contain a sandbox-requiring command for sandbox-coordinator
        // to actually invoke the factory; CheckoutSourceCommand is the canonical example.
        var commands = new[] { CommandNames.CheckoutSource };
        // Executor reads Repos from the pipeline context for sandbox-language
        // resolution; tests must seed it explicitly.
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });

        var act = async () => await h.Sut.ExecuteAsync(
            commands, new ResolvedProject(), pipeline, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        h.LifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AllCommandsSucceed_DoesNotMarkLifecycleFailed()
    {
        var h = new PipelineExecutorTestBuilder();
        var result = await h.Sut.ExecuteAsync(
            Array.Empty<string>(), new ResolvedProject(), new PipelineContext(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        h.LifecycleMock.Verify(l => l.MarkFailed(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CommandReturnsFail_MarksLifecycleFailed()
    {
        var h = new PipelineExecutorTestBuilder();
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
