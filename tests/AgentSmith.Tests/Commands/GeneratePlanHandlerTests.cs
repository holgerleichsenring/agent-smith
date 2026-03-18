using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
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
    private readonly Mock<IDecisionLogger> _decisionLoggerMock = new();
    private readonly GeneratePlanHandler _handler;

    public GeneratePlanHandlerTests()
    {
        _handler = new GeneratePlanHandler(
            _factoryMock.Object,
            _decisionLoggerMock.Object,
            NullLoggerFactory.Instance.CreateLogger<GeneratePlanHandler>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_StoresPlanInPipeline()
    {
        var plan = new Plan("Test plan", new List<PlanStep>(), "{}");
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.GeneratePlanAsync(
                It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<Plan>(ContextKeys.Plan).Should().Be(plan);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_PropagatesException()
    {
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.GeneratePlanAsync(
                It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);

        var act = async () => await _handler.ExecuteAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("API error");
    }

    [Fact]
    public async Task ExecuteAsync_ConsolidatedPlanExists_SkipsGeneration()
    {
        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ConsolidatedPlan, "Already consolidated plan content");

        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("consolidated");
        _factoryMock.Verify(f => f.Create(It.IsAny<AgentConfig>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoConsolidatedPlan_GeneratesNormally()
    {
        var plan = new Plan("Test plan", new List<PlanStep>(), "{}");
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.GeneratePlanAsync(
                It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<Plan>(ContextKeys.Plan, out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PlanWithDecisions_WritesViaDecisionLogger()
    {
        var decisions = new List<PlanDecision>
        {
            new("Architecture", "**Redis Streams**: fan-out required"),
            new("Tooling", "**DuckDB**: reads Parquet natively")
        };
        var plan = new Plan("Test plan", new List<PlanStep>(), "{}", decisions);
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.GeneratePlanAsync(
                It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        var repo = new Repository("/tmp/repo", new BranchName("main"), "https://github.com/org/repo.git");
        pipeline.Set(ContextKeys.Repository, repo);
        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        _decisionLoggerMock.Verify(d => d.LogAsync(
            "/tmp/repo", DecisionCategory.Architecture,
            "**Redis Streams**: fan-out required",
            It.IsAny<CancellationToken>()), Times.Once);
        _decisionLoggerMock.Verify(d => d.LogAsync(
            "/tmp/repo", DecisionCategory.Tooling,
            "**DuckDB**: reads Parquet natively",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PlanWithDecisions_NoRepoInPipeline_StillLogsWithNullRepoPath()
    {
        var decisions = new List<PlanDecision>
        {
            new("Architecture", "**Redis**: reason")
        };
        var plan = new Plan("Test plan", new List<PlanStep>(), "{}", decisions);
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.GeneratePlanAsync(
                It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _decisionLoggerMock.Verify(d => d.LogAsync(
            null, DecisionCategory.Architecture,
            "**Redis**: reason",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PlanWithDecisions_StoresDecisionsInPipeline()
    {
        var decisions = new List<PlanDecision>
        {
            new("Architecture", "**Redis**: reason"),
            new("Tooling", "**DuckDB**: fast reads")
        };
        var plan = new Plan("Test plan", new List<PlanStep>(), "{}", decisions);
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.GeneratePlanAsync(
                It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        pipeline.TryGet<List<PlanDecision>>(ContextKeys.Decisions, out var stored).Should().BeTrue();
        stored.Should().HaveCount(2);
    }
}
