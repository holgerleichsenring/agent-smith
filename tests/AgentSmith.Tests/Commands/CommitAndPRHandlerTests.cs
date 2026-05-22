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

public class CommitAndPRHandlerTests
{
    private readonly Mock<ISourceProviderFactory> _sourceFactoryMock = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
    private readonly Mock<ISourceProvider> _sourceProviderMock = new();
    private readonly Mock<ITicketProvider> _ticketProviderMock = new();
    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly CommitAndPRHandler _sut;

    public CommitAndPRHandlerTests()
    {
        _sourceFactoryMock.Setup(f => f.Create(It.IsAny<RepoConnection>()))
            .Returns(_sourceProviderMock.Object);
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(_ticketProviderMock.Object);

        _sourceProviderMock.Setup(s => s.CreatePullRequestAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<TicketId?>()))
            .ReturnsAsync("https://github.com/test/repo/pull/42");

        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));

        _sut = new CommitAndPRHandler(
            _sourceFactoryMock.Object,
            _ticketFactoryMock.Object,
            new SandboxGitOperations(NullLogger<SandboxGitOperations>.Instance),
            NullLogger<CommitAndPRHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesCommitAndPR_ThenClosesTicket()
    {
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("pull/42");

        _sandboxMock.Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git" && st.Args!.Contains("commit")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _sandboxMock.Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git" && st.Args!.Contains("push")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Once);

        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.Is<TicketId>(id => id.Value == "123"),
            It.Is<string>(s => s.Contains("Agent Smith") && s.Contains("pull/42")),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoSandboxInPipelineContext_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Sandboxes");
    }

    [Fact]
    public async Task ExecuteAsync_TicketCloseFailure_StillReturnsPRSuccess()
    {
        _ticketProviderMock.Setup(t => t.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("pull/42");
    }

    [Fact]
    public async Task ExecuteAsync_IncludesChangeListInSummary()
    {
        string? postedSummary = null;
        _ticketProviderMock.Setup(t => t.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<TicketId, string, string?, CancellationToken>((_, summary, _, _) => postedSummary = summary)
            .Returns(Task.CompletedTask);

        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        postedSummary.Should().NotBeNull();
        postedSummary.Should().Contain("README.md");
        postedSummary.Should().Contain("Created");
    }

    [Fact]
    public async Task ExecuteAsync_WithDoneStatus_FinalizesAtomicallyWithStatus()
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.DoneStatus, "In Review");
        var context = CreateContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // Comment + status transition collapsed into ONE provider call so AzDO
        // can emit a single PATCH and avoid the TF26071 rev race.
        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.Is<TicketId>(id => id.Value == "123"),
            It.Is<string>(s => s.Contains("Agent Smith")),
            "In Review",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutDoneStatus_FinalizesAtomicallyWithNullStatus()
    {
        var context = CreateContext();

        await _sut.ExecuteAsync(context, CancellationToken.None);

        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(),
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private CommitAndPRContext CreateContext(PipelineContext? pipeline = null)
    {
        var pl = pipeline ?? NewPipelineWithSandbox();
        var repo = new Repository(new BranchName("fix/123"), "https://github.com/test/repo");
        var ticket = new Ticket(new TicketId("123"), "Fix the bug", "Description", null, "Open", "GitHub");
        var changes = new List<CodeChange>
        {
            new(new FilePath("README.md"), "content", "Created")
        };
        return new CommitAndPRContext(repo, changes, ticket, new[] { new RepoConnection() }, new TrackerConnection(), pl);
    }

    private void SeedSandboxes(PipelineContext pipeline)
    {
        pipeline.Set(ContextKeys.Sandbox, _sandboxMock.Object);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [string.Empty] = _sandboxMock.Object });
    }

    private PipelineContext NewPipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        SeedSandboxes(pipeline);
        return pipeline;
    }
}
