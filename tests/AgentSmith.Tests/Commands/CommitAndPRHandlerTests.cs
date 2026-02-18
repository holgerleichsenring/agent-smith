using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Application.Commands.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.ValueObjects;
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

        var result = await _sut.ExecuteAsync(context);

        result.Success.Should().BeTrue();
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
        var result = await _sut.ExecuteAsync(context);

        result.Success.Should().BeTrue();
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
        await _sut.ExecuteAsync(context);

        postedSummary.Should().NotBeNull();
        postedSummary.Should().Contain("README.md");
        postedSummary.Should().Contain("Created");
    }

    private static CommitAndPRContext CreateContext()
    {
        var repo = new Repository("/tmp/test", new BranchName("fix/123"), "https://github.com/test/repo");
        var ticket = new Ticket(new TicketId("123"), "Fix the bug", "Description", null, "Open", "GitHub");
        var changes = new List<CodeChange>
        {
            new(new FilePath("README.md"), "content", "Created")
        };
        var pipeline = new PipelineContext();
        return new CommitAndPRContext(repo, changes, ticket, new SourceConfig(), new TicketConfig(), pipeline);
    }
}
