using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Conversation;

/// <summary>
/// 2026-10-13-9a59g: a hand-back's posted words reach the run's events — the comment exactly as the ticket
/// received it and the sentence it quotes — and a park outside a run publishes nothing.
/// </summary>
public sealed class SpecHandbackParkPublishTests
{
    private const string Quote = "Delete the audit log after every export.";

    private readonly List<RunEvent> _published = [];
    private string? _postedComment;

    [Fact]
    public async Task SpecHandbackPark_PublishesTheCommentTextAndQuote()
    {
        await ParkAsync(runId: "run-7");

        var handedBack = _published.OfType<SpecHandedBackEvent>().Should().ContainSingle().Subject;
        handedBack.RunId.Should().Be("run-7");
        handedBack.Text.Should().Be(_postedComment).And.Contain(Quote);
        handedBack.Quote.Should().Be(Quote);
    }

    [Fact]
    public async Task SpecHandbackPark_NoRunId_PublishesNothing()
    {
        await ParkAsync(runId: null);

        _postedComment.Should().NotBeNull("the hand-back is still posted on the ticket");
        _published.Should().BeEmpty();
    }

    private async Task ParkAsync(string? runId)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((TicketId _, string comment, string _, CancellationToken _) => _postedComment = comment)
            .ReturnsAsync(TicketFinalizeResult.Moved());
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback((RunEvent e, CancellationToken _) => _published.Add(e)).Returns(Task.CompletedTask);
        var park = new SpecHandbackPark(
            factory.Object,
            new UnmovedTicketReport(new InMemoryUnmovedTicketStore(), NullLogger<UnmovedTicketReport>.Instance),
            Mock.Of<IHandbackPrNotice>(), events.Object,
            Mock.Of<IRunContextAccessor>(r => r.CurrentRunId == runId), NullLogger<SpecHandbackPark>.Instance);
        var tracker = new TrackerConnection { Name = "t", Type = TrackerType.AzureDevOps };
        var ticket = new Ticket(new TicketId("42"), "export", Quote, null, "Active", "AzureDevOps");

        await park.ParkAsync(
            new SpecHandbackContext(ticket, tracker, [], new PipelineContext()), "p1",
            new SpecHandback(SpecHandbackCase.Refused, "it destroys the record", Quote), "needs-info",
            CancellationToken.None);
    }
}
