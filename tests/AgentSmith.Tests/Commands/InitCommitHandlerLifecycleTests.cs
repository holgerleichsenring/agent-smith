using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0133: InitCommitHandler gains an optional ticket-lifecycle branch — when a
/// TicketId is in pipeline context (label-triggered init), the handler
/// transitions the ticket to done_status (or closes it) and posts a PR-link
/// summary, identical to the lifecycle CommitAndPRHandler runs. Slack-modal /
/// CLI init paths publish no TicketId; the lifecycle branch then no-ops.
/// </summary>
public sealed class InitCommitHandlerLifecycleTests
{
    private readonly Mock<ISourceProviderFactory> _sourceFactoryMock = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
    private readonly Mock<ISourceProvider> _sourceProviderMock = new();
    private readonly Mock<ITicketProvider> _ticketProviderMock = new();
    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly InitCommitHandler _sut;

    public InitCommitHandlerLifecycleTests()
    {
        _sourceFactoryMock.Setup(f => f.Create(It.IsAny<RepoConnection>()))
            .Returns(_sourceProviderMock.Object);
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(_ticketProviderMock.Object);

        _sourceProviderMock.Setup(s => s.CreatePullRequestAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<TicketId?>()))
            .ReturnsAsync("https://github.com/test/repo/pull/7");

        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));

        _sut = new InitCommitHandler(
            _sourceFactoryMock.Object,
            _ticketFactoryMock.Object,
            new SandboxGitOperations(NullLogger<SandboxGitOperations>.Instance),
            NullLogger<InitCommitHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoTicketIdInPipeline_SkipsLifecycleAndStillCommitsPR()
    {
        var context = CreateContext(NewPipelineWithSandbox());

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("pull/7");

        _ticketProviderMock.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ticketProviderMock.Verify(t => t.TransitionToAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ticketProviderMock.Verify(t => t.CloseTicketAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ticketFactoryMock.Verify(f => f.Create(It.IsAny<TrackerConnection>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_TicketIdWithDoneStatus_TransitionsAndPostsPRLinkSummary()
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));
        pipeline.Set(ContextKeys.DoneStatus, "closed");
        var context = CreateContext(pipeline);

        string? postedSummary = null;
        _ticketProviderMock.Setup(t => t.UpdateStatusAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TicketId, string, CancellationToken>((_, s, _) => postedSummary = s)
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        _ticketProviderMock.Verify(t => t.UpdateStatusAsync(
            It.Is<TicketId>(id => id.Value == "42"),
            It.Is<string>(s => s.Contains("pull/7")),
            It.IsAny<CancellationToken>()), Times.Once);
        _ticketProviderMock.Verify(t => t.TransitionToAsync(
            It.Is<TicketId>(id => id.Value == "42"), "closed",
            It.IsAny<CancellationToken>()), Times.Once);
        _ticketProviderMock.Verify(t => t.CloseTicketAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        postedSummary.Should().Contain("pull/7");
        postedSummary.Should().Contain("Init Complete");
    }

    [Fact]
    public async Task ExecuteAsync_TicketIdWithoutDoneStatus_ClosesTicketWithPRLinkSummary()
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.TicketId, new TicketId("99"));
        var context = CreateContext(pipeline);

        string? postedSummary = null;
        _ticketProviderMock.Setup(t => t.CloseTicketAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TicketId, string, CancellationToken>((_, s, _) => postedSummary = s)
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        _ticketProviderMock.Verify(t => t.CloseTicketAsync(
            It.Is<TicketId>(id => id.Value == "99"),
            It.Is<string>(s => s.Contains("pull/7")),
            It.IsAny<CancellationToken>()), Times.Once);
        _ticketProviderMock.Verify(t => t.TransitionToAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ticketProviderMock.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        postedSummary.Should().Contain("pull/7");
    }

    [Fact]
    public async Task ExecuteAsync_TicketTransitionFails_StillReturnsPRSuccess()
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));
        pipeline.Set(ContextKeys.DoneStatus, "closed");
        var context = CreateContext(pipeline);

        _ticketProviderMock.Setup(t => t.TransitionToAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("pull/7");
    }

    private InitCommitContext CreateContext(PipelineContext pipeline)
    {
        var repo = new Repository(new BranchName("agentsmith/init"), "https://github.com/test/repo");
        return new InitCommitContext(repo, new RepoConnection(), new TrackerConnection(), pipeline);
    }

    private PipelineContext NewPipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, _sandboxMock.Object);
        return pipeline;
    }
}
