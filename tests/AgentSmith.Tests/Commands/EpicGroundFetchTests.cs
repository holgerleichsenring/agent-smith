using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-09-13-7d9f: a run whose ticket is one slice of an epic reads the epic itself, so
/// every child of one cut is derived against the same ground. The parent is addressed by
/// the <c>phase-parent:</c> stamp (2026-09-13-a72a), read once, with the ticket — and a
/// parent that cannot be read is named on the run, which then proceeds on its own ticket.
/// </summary>
public sealed class EpicGroundFetchTests
{
    private const string ParentBody = "## Goal\nOne vocabulary for the widget service.\n";

    private readonly Mock<ITicketProviderFactory> _factory = new();
    private readonly Mock<ITicketProvider> _provider = new();
    private readonly FetchTicketHandler _handler;

    public EpicGroundFetchTests()
    {
        _factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_provider.Object);
        _handler = new FetchTicketHandler(
            _factory.Object,
            Mock.Of<IEventPublisher>(),
            Mock.Of<IRunContextAccessor>(),
            new TicketExtrasFetcher(NullLogger<TicketExtrasFetcher>.Instance),
            new EpicGroundFetcher(NullLogger<EpicGroundFetcher>.Instance),
            NullLogger<FetchTicketHandler>.Instance);
    }

    [Fact]
    public async Task Run_TicketCarriesParentLabel_FetchesItOnce()
    {
        var pipeline = await RunAsync(Child(FiledTicketLabels.ParentStamp("4711")), Parent());

        var ground = pipeline.Get<EpicGround>(ContextKeys.EpicGround);
        ground.ParentTicketId.Should().Be("4711");
        ground.Title.Should().Be("Widget service");
        ground.Body.Should().Be(ParentBody);
        _provider.Verify(
            p => p.GetTicketAsync(It.Is<TicketId>(t => t.Value == "4711"), It.IsAny<CancellationToken>()),
            Times.Once, "the epic is read once, with the ticket, and never again mid-run");
    }

    [Fact]
    public async Task Run_TicketHasNoParentLabel_FetchUnchanged()
    {
        var pipeline = await RunAsync(Child("bug"), Parent());

        pipeline.TryGet<EpicGround>(ContextKeys.EpicGround, out _).Should().BeFalse(
            "a ticket that is nobody's slice produces the derivation prompt it always did");
        _provider.Verify(
            p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()),
            Times.Once, "only the run's own ticket is fetched");
    }

    [Fact]
    public async Task Run_ParentUnfetchable_ReportedAndRunProceeds()
    {
        var child = Child(FiledTicketLabels.ParentStamp("4711"));
        _provider.Setup(p => p.GetTicketAsync(
                It.Is<TicketId>(t => t.Value == "42"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(child);
        _provider.Setup(p => p.GetTicketAsync(
                It.Is<TicketId>(t => t.Value == "4711"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("404 - the epic was deleted"));

        var pipeline = new PipelineContext();
        var result = await _handler.ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "the child's own ticket is a complete requirement; an epic that is gone is reported");
        result.Message.Should().Contain("4711").And.Contain("could not be read");
        pipeline.TryGet<EpicGround>(ContextKeys.EpicGround, out _).Should().BeFalse();
        pipeline.Get<Ticket>(ContextKeys.Ticket).Should().Be(child);
    }

    private async Task<PipelineContext> RunAsync(Ticket child, Ticket parent)
    {
        _provider.Setup(p => p.GetTicketAsync(
                It.Is<TicketId>(t => t.Value == "42"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(child);
        _provider.Setup(p => p.GetTicketAsync(
                It.Is<TicketId>(t => t.Value == "4711"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        var pipeline = new PipelineContext();
        var result = await _handler.ExecuteAsync(Context(pipeline), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return pipeline;
    }

    private static FetchTicketContext Context(PipelineContext pipeline) =>
        new(new TicketId("42"), new TrackerConnection { Type = TrackerType.GitHub }, pipeline);

    private static Ticket Child(params string[] labels) =>
        new(new TicketId("42"), "Widget storage", "The table and the repository.",
            null, "Open", "test", labels);

    private static Ticket Parent() =>
        new(new TicketId("4711"), "Widget service", ParentBody, null, "Open", "test",
            ["phase-epic"]);
}
