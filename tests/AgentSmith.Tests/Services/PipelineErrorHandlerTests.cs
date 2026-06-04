using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0147e: per-service unit tests for IPipelineErrorHandler. Covers the
/// failure policy: HTML-formatted ticket comment, WIP-persist read-only guard,
/// lifecycle.MarkFailed, and the "working" status posting.
/// </summary>
public sealed class PipelineErrorHandlerTests
{
    private readonly Mock<ICommandExecutor> _executorMock = new();
    private readonly Mock<ICommandContextFactory> _factoryMock = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
    private readonly Mock<IAsyncPipelineLifecycle> _lifecycleMock = new();
    private readonly PipelineErrorHandler _sut;

    public PipelineErrorHandlerTests()
    {
        _sut = new PipelineErrorHandler(
            _executorMock.Object,
            _factoryMock.Object,
            _ticketFactoryMock.Object,
            NullLogger<PipelineErrorHandler>.Instance);
    }

    [Fact]
    public async Task HandleStepFailureAsync_PostsHtmlFormattedComment_AndMarksLifecycleFailed()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(ticketProvider.Object);

        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));
        var failure = CommandResult.Fail("boom") with
        {
            FailedStep = 2,
            TotalSteps = 3,
            StepName = "Test"
        };

        await _sut.HandleStepFailureAsync(
            Array.Empty<string>(),
            new ResolvedProject(),
            context,
            _lifecycleMock.Object,
            failure,
            CancellationToken.None);

        ticketProvider.Verify(t => t.UpdateStatusAsync(
            It.Is<TicketId>(id => id.Value == "99"),
            It.Is<string>(s =>
                s.Contains("<b>Agent Smith — Failed</b>")
                && s.Contains("<b>Step:</b> Test (2/3)")
                && s.Contains("<b>Error:</b> boom")),
            It.IsAny<CancellationToken>()), Times.Once);
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_NoTicketId_DoesNotCallTicketFactory_ButStillMarksFailed()
    {
        await _sut.HandleStepFailureAsync(
            Array.Empty<string>(),
            new ResolvedProject(),
            new PipelineContext(),
            _lifecycleMock.Object,
            CommandResult.Fail("boom"),
            CancellationToken.None);

        _ticketFactoryMock.Verify(f => f.Create(It.IsAny<TrackerConnection>()), Times.Never);
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_ReadOnlyPipelineWithRepository_DoesNotPersistWorkBranch()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://example.com/repo.git"));

        // Pipeline contains no AgenticExecute / GenerateTests / GenerateDocs.
        var commands = new[] { CommandNames.SpawnNuclei, CommandNames.DeliverFindings };

        await _sut.HandleStepFailureAsync(
            commands, new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("scan failed"),
            CancellationToken.None);

        _factoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleStepFailureAsync_CodeModifyingPipelineWithRepository_AttemptsPersistWorkBranch()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://example.com/repo.git"));
        var commands = new[] { CommandNames.AgenticExecute, CommandNames.WriteRunResult };

        await _sut.HandleStepFailureAsync(
            commands, new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("test failed"),
            CancellationToken.None);

        _factoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_AgenticMasterPipelineWithRepository_AttemptsPersistWorkBranch()
    {
        // p0202c: fix-no-test / add-feature run AgenticMaster (the post-p0179b
        // coding handler) and carry no explicit PersistWorkBranch step. The
        // failure-recovery path must still persist the master's edits.
        var context = new PipelineContext();
        context.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://example.com/repo.git"));
        var commands = new[] { CommandNames.AgenticMaster, CommandNames.WriteRunResult };

        await _sut.HandleStepFailureAsync(
            commands, new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("test failed"),
            CancellationToken.None);

        _factoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_PersistThrows_DoesNotMaskOriginalFailure_AndStillMarksFailed()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://example.com/repo.git"));
        var commands = new[] { CommandNames.AgenticExecute };
        _factoryMock.Setup(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()))
            .Throws(new InvalidOperationException("persist broke"));

        var act = async () => await _sut.HandleStepFailureAsync(
            commands, new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("agentic failed"),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Fact]
    public async Task PostWorkingStatusAsync_PostsWorkingMessageWhenTicketIdPresent()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("42"));

        await _sut.PostWorkingStatusAsync(new ResolvedProject(), context, CancellationToken.None);

        ticketProvider.Verify(t => t.UpdateStatusAsync(
            It.Is<TicketId>(id => id.Value == "42"),
            It.Is<string>(s => s.Contains("working on")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
