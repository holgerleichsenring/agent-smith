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

public sealed class GeneratePlanHandlerTests
{
    private readonly Mock<IAgentProviderFactory> _factoryMock = new();
    private readonly GeneratePlanHandler _handler;

    public GeneratePlanHandlerTests()
    {
        _handler = new GeneratePlanHandler(
            _factoryMock.Object,
            NullLoggerFactory.Instance.CreateLogger<GeneratePlanHandler>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_StoresPlanInPipeline()
    {
        var plan = new Plan("Test plan", new List<PlanStep>(), "{}");
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.GeneratePlanAsync(
                It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);

        var result = await _handler.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<Plan>(ContextKeys.Plan).Should().Be(plan);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_PropagatesException()
    {
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.GeneratePlanAsync(
                It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);

        var act = async () => await _handler.ExecuteAsync(context);

        await act.Should().ThrowAsync<Exception>().WithMessage("API error");
    }
}
