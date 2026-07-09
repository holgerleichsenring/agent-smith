using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class FetchTicketHandlerTests
{
    private readonly Mock<ITicketProviderFactory> _factoryMock = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<IRunContextAccessor> _runContext = new();
    private readonly FetchTicketHandler _handler;
    private readonly List<RunEvent> _publishedEvents = new();

    public FetchTicketHandlerTests()
    {
        _eventPublisher
            .Setup(p => p.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Returns((RunEvent e, CancellationToken _) => { _publishedEvents.Add(e); return Task.CompletedTask; });
        _runContext.SetupGet(r => r.CurrentRunId).Returns("2026-05-31T20-11-07-fb74");

        _handler = new FetchTicketHandler(
            _factoryMock.Object,
            _eventPublisher.Object,
            _runContext.Object,
            NullLoggerFactory.Instance.CreateLogger<FetchTicketHandler>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_StoresTicketInPipeline()
    {
        var ticketId = new TicketId("42");
        var ticket = new Ticket(ticketId, "Fix bug", "Fix the login bug", null, "Open", "github");
        var providerMock = new Mock<ITicketProvider>();
        providerMock.Setup(p => p.GetTicketAsync(
                It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _factoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, ticketId);
        var context = new FetchTicketContext(ticketId, new TrackerConnection { Type = TrackerType.GitHub }, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<Ticket>(ContextKeys.Ticket).Should().Be(ticket);
    }

    [Fact]
    public async Task FetchTicketHandler_AfterFetch_PublishesTicketFetchedEvent()
    {
        var ticketId = new TicketId("18794");
        var ticket = new Ticket(
            ticketId, "Fix refresh-token expiry", "Long description body…",
            "Should return 401 on expired tokens", "Active", "azuredevops",
            labels: ["Sample", "agent-smith:bug"]);
        var providerMock = new Mock<ITicketProvider>();
        providerMock.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        providerMock.SetupGet(p => p.ProviderType).Returns("AzureDevOps");
        _factoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, ticketId);
        var context = new FetchTicketContext(ticketId, new TrackerConnection { Type = TrackerType.AzureDevOps }, pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        _publishedEvents.Should().ContainSingle(e => e is TicketFetchedEvent);
        var ev = (TicketFetchedEvent)_publishedEvents.Single(e => e is TicketFetchedEvent);
        ev.TicketId.Should().Be("18794");
        ev.Title.Should().Be("Fix refresh-token expiry");
        ev.State.Should().Be("Active");
        ev.Labels.Should().BeEquivalentTo(new[] { "Sample", "agent-smith:bug" });
        ev.Source.Should().Be("azuredevops");
        ev.AttachmentCount.Should().Be(0);
    }

    [Fact]
    public async Task FetchTicketHandler_NoRunIdInContext_SkipsEventPublish()
    {
        _runContext.SetupGet(r => r.CurrentRunId).Returns((string?)null);
        var ticketId = new TicketId("99");
        var ticket = new Ticket(ticketId, "t", "d", null, "Open", "github");
        var providerMock = new Mock<ITicketProvider>();
        providerMock.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _factoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(providerMock.Object);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, ticketId);
        var context = new FetchTicketContext(ticketId, new TrackerConnection { Type = TrackerType.GitHub }, pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        _publishedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchTicket_CommentsFetched_PublishedToContext()
    {
        var ticketId = new TicketId("42");
        var ticket = new Ticket(ticketId, "Fix bug", "desc", null, "Open", "github");
        var comments = new List<TicketComment>
        {
            new("jane", new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero), "use approach B, not A"),
        };
        var providerMock = new Mock<ITicketProvider>();
        providerMock.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        providerMock.Setup(p => p.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comments);
        _factoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, ticketId);
        var context = new FetchTicketContext(ticketId, new TrackerConnection { Type = TrackerType.GitHub }, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments)
            .Should().BeEquivalentTo(comments);
    }

    [Fact]
    public async Task FetchTicket_CommentFetchFails_RunContinuesWithoutComments()
    {
        var ticketId = new TicketId("42");
        var ticket = new Ticket(ticketId, "Fix bug", "desc", null, "Open", "github");
        var providerMock = new Mock<ITicketProvider>();
        providerMock.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        providerMock.Setup(p => p.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("comments endpoint down"));
        _factoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, ticketId);
        var context = new FetchTicketContext(ticketId, new TrackerConnection { Type = TrackerType.GitHub }, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a run without the conversation beats no run");
        pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out _)
            .Should().BeFalse();
    }

    [Fact]
    public async Task FetchTicket_DocumentsDownloaded_PublishedToContext()
    {
        var ticketId = new TicketId("42");
        var ticket = new Ticket(ticketId, "Fix bug", "desc", null, "Open", "github");
        var documents = new List<TicketDocumentAttachment>
        {
            new(new AttachmentRef("https://x/spec.docx", "spec.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"), [1, 2]),
        };
        var providerMock = new Mock<ITicketProvider>();
        providerMock.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        providerMock.Setup(p => p.DownloadDocumentAttachmentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);
        _factoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, ticketId);
        var context = new FetchTicketContext(ticketId, new TrackerConnection { Type = TrackerType.GitHub }, pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        pipeline.Get<IReadOnlyList<TicketDocumentAttachment>>(ContextKeys.TicketDocuments)
            .Should().BeEquivalentTo(documents);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_PropagatesException()
    {
        var providerMock = new Mock<ITicketProvider>();
        providerMock.Setup(p => p.GetTicketAsync(
                It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));
        _factoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(providerMock.Object);

        var ticketId = new TicketId("42");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, ticketId);
        var context = new FetchTicketContext(ticketId, new TrackerConnection { Type = TrackerType.GitHub }, pipeline);

        var act = async () => await _handler.ExecuteAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("API error");
    }
}
