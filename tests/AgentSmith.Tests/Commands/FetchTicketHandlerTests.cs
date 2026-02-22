using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
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
    private readonly FetchTicketHandler _handler;

    public FetchTicketHandlerTests()
    {
        _handler = new FetchTicketHandler(
            _factoryMock.Object,
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
        _factoryMock.Setup(f => f.Create(It.IsAny<TicketConfig>()))
            .Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, ticketId);
        var context = new FetchTicketContext(ticketId, new TicketConfig { Type = "github" }, pipeline);

        var result = await _handler.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<Ticket>(ContextKeys.Ticket).Should().Be(ticket);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_PropagatesException()
    {
        var providerMock = new Mock<ITicketProvider>();
        providerMock.Setup(p => p.GetTicketAsync(
                It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));
        _factoryMock.Setup(f => f.Create(It.IsAny<TicketConfig>()))
            .Returns(providerMock.Object);

        var ticketId = new TicketId("42");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, ticketId);
        var context = new FetchTicketContext(ticketId, new TicketConfig { Type = "github" }, pipeline);

        var act = async () => await _handler.ExecuteAsync(context);

        await act.Should().ThrowAsync<Exception>().WithMessage("API error");
    }
}
