using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
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
    private readonly CommitAndPRHandler _sut;

    public CommitAndPRHandlerTests()
    {
        _sourceFactoryMock.Setup(f => f.Create(It.IsAny<SourceConfig>()))
            .Returns(_sourceProviderMock.Object);
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TicketConfig>()))
            .Returns(_ticketProviderMock.Object);

        _sourceProviderMock.Setup(s => s.CreatePullRequestAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://github.com/test/repo/pull/42");

        _sut = new CommitAndPRHandler(
            _sourceFactoryMock.Object,
            _ticketFactoryMock.Object,
            NullLogger<CommitAndPRHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesCommitAndPR_ThenClosesTicket()
    {
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("pull/42");

        _sourceProviderMock.Verify(s => s.CommitAndPushAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        _ticketProviderMock.Verify(t => t.CloseTicketAsync(
            It.Is<TicketId>(id => id.Value == "123"),
            It.Is<string>(s => s.Contains("Agent Smith") && s.Contains("pull/42")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TicketCloseFailure_StillReturnsPRSuccess()
    {
        _ticketProviderMock.Setup(t => t.CloseTicketAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
        _ticketProviderMock.Setup(t => t.CloseTicketAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TicketId, string, CancellationToken>((_, summary, _) => postedSummary = summary)
            .Returns(Task.CompletedTask);

        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        postedSummary.Should().NotBeNull();
        postedSummary.Should().Contain("README.md");
        postedSummary.Should().Contain("Created");
    }

    [Fact]
    public async Task ExecuteAsync_WithDoneStatus_TransitionsInsteadOfClosing()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.DoneStatus, "In Review");
        var context = CreateContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        _ticketProviderMock.Verify(t => t.TransitionToAsync(
            It.Is<TicketId>(id => id.Value == "123"),
            "In Review",
            It.IsAny<CancellationToken>()), Times.Once);

        _ticketProviderMock.Verify(t => t.UpdateStatusAsync(
            It.Is<TicketId>(id => id.Value == "123"),
            It.Is<string>(s => s.Contains("Agent Smith")),
            It.IsAny<CancellationToken>()), Times.Once);

        _ticketProviderMock.Verify(t => t.CloseTicketAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutDoneStatus_ClosesTicket()
    {
        var context = CreateContext();

        await _sut.ExecuteAsync(context, CancellationToken.None);

        _ticketProviderMock.Verify(t => t.CloseTicketAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        _ticketProviderMock.Verify(t => t.TransitionToAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CommitAndPRContext CreateContext(PipelineContext? pipeline = null)
    {
        var repo = new Repository("/tmp/test", new BranchName("fix/123"), "https://github.com/test/repo");
        var ticket = new Ticket(new TicketId("123"), "Fix the bug", "Description", null, "Open", "GitHub");
        var changes = new List<CodeChange>
        {
            new(new FilePath("README.md"), "content", "Created")
        };
        return new CommitAndPRContext(repo, changes, ticket, new SourceConfig(), new TicketConfig(), pipeline ?? new PipelineContext());
    }
}
