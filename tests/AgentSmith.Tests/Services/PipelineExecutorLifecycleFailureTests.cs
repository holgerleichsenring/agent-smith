using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
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
    public async Task ExecuteAsync_SandboxFactoryThrows_TerminalizesNativeTicketStatus()
    {
        // p0269: a thrown sandbox-spawn failure (e.g. k8s ResourceQuota Forbidden)
        // aborts BEFORE any step returns a failure CommandResult, so the step-failure
        // path that moves the native ticket status never runs. Regression guard: the
        // exception path must still terminalize the ticket (FinalizeAsync with the
        // configured failed_status) — otherwise it stays in trigger_statuses and the
        // poller re-claims it every cycle (the every-minute re-trigger loop).
        var h = new PipelineExecutorTestBuilder();
        h.SandboxFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("exceeded quota: pods"));

        var ticketProviderMock = new Mock<ITicketProvider>();
        h.TicketFactoryMock
            .Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(ticketProviderMock.Object);

        var commands = new[] { CommandNames.CheckoutSource };
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });
        pipeline.Set(ContextKeys.TicketId, new TicketId("18845"));
        pipeline.Set(ContextKeys.FailedStatus, "Failed");

        var act = async () => await h.Sut.ExecuteAsync(
            commands, new ResolvedProject(), pipeline, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        ticketProviderMock.Verify(
            p => p.FinalizeAsync(
                It.Is<TicketId>(t => t.Value == "18845"),
                It.IsAny<string>(),
                "Failed",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CapacityExhausted_DoesNotTerminalizeNativeTicketStatus()
    {
        // p0269a: a CapacityExhaustedException is NOT a fatal failure — the run did not
        // fit right now. The native ticket status must be LEFT in trigger_statuses so
        // the ticket is reclaimable and re-runs when capacity frees; only the lifecycle
        // tag is marked. Every OTHER thrown exception still terminalizes (the test above).
        var h = new PipelineExecutorTestBuilder();
        h.SandboxFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AgentSmith.Domain.Exceptions.CapacityExhaustedException(
                "agentsmith", "requests.cpu", "exceeded quota: compute"));

        var ticketProviderMock = new Mock<ITicketProvider>();
        h.TicketFactoryMock
            .Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(ticketProviderMock.Object);

        var commands = new[] { CommandNames.CheckoutSource };
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { new RepoConnection() });
        pipeline.Set(ContextKeys.TicketId, new TicketId("18846"));
        pipeline.Set(ContextKeys.FailedStatus, "Failed");

        var act = async () => await h.Sut.ExecuteAsync(
            commands, new ResolvedProject(), pipeline, CancellationToken.None);

        await act.Should().ThrowAsync<AgentSmith.Domain.Exceptions.CapacityExhaustedException>();
        ticketProviderMock.Verify(
            p => p.FinalizeAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "a capacity rejection must not terminalize the ticket");
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
