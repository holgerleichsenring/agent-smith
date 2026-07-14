using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0326: an inline ticket on the run context IS the requirement record — the
/// handler materializes it and never touches the provider factory, so the demo
/// runs the real fix-bug preset with zero tracker configuration.
/// </summary>
public sealed class FetchTicketInlineTests
{
    [Fact]
    public async Task FetchTicket_InlinePayload_SkipsProviderLookup()
    {
        var factory = new Mock<ITicketProviderFactory>(MockBehavior.Strict); // Create() would throw
        var handler = new FetchTicketHandler(
            factory.Object,
            Mock.Of<IEventPublisher>(),
            Mock.Of<IRunContextAccessor>(),
            NullLoggerFactory.Instance.CreateLogger<FetchTicketHandler>());

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.InlineTicket, new InlineTicket(
            "Bulk discount boundary bug", "Orders of exactly 100.00 miss the discount.", "dotnet test fails"));
        var context = new FetchTicketContext(
            TicketId: null, new TrackerConnection { Type = TrackerType.GitHub }, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        factory.Verify(f => f.Create(It.IsAny<TrackerConnection>()), Times.Never);
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        ticket.Title.Should().Be("Bulk discount boundary bug");
        ticket.Source.Should().Be(InlineTicket.Source);
        ticket.Description.Should().Contain("Reproduction:").And.Contain("dotnet test fails");
    }

    [Fact]
    public async Task FetchTicket_InlinePayload_PublishesTicketFetchedEvent()
    {
        var events = new List<RunEvent>();
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Returns((RunEvent e, CancellationToken _) => { events.Add(e); return Task.CompletedTask; });
        var runContext = new Mock<IRunContextAccessor>();
        runContext.SetupGet(r => r.CurrentRunId).Returns("2026-07-14T10-00-00-demo");
        var handler = new FetchTicketHandler(
            Mock.Of<ITicketProviderFactory>(), publisher.Object, runContext.Object,
            NullLoggerFactory.Instance.CreateLogger<FetchTicketHandler>());

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.InlineTicket, new InlineTicket("Inline title", "Body"));
        await handler.ExecuteAsync(
            new FetchTicketContext(null, new TrackerConnection(), pipeline), CancellationToken.None);

        events.OfType<TicketFetchedEvent>().Should().ContainSingle()
            .Which.Title.Should().Be("Inline title");
    }
}
