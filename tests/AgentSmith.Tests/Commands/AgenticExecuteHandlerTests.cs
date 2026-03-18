using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class AgenticExecuteHandlerTests
{
    private readonly Mock<IAgentProviderFactory> _factoryMock = new();
    private readonly Mock<IDecisionLogger> _decisionLoggerMock = new();
    private readonly Mock<IProgressReporter> _reporterMock = new();
    private readonly AgenticExecuteHandler _handler;

    public AgenticExecuteHandlerTests()
    {
        _handler = new AgenticExecuteHandler(
            _factoryMock.Object,
            _decisionLoggerMock.Object,
            _reporterMock.Object,
            NullLoggerFactory.Instance.CreateLogger<AgenticExecuteHandler>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_StoresCodeChangesInPipeline()
    {
        var changes = new List<CodeChange>
        {
            new(new FilePath("file.cs"), "content", "Create")
        };
        var executionResult = new AgentExecutionResult(changes, null, null);
        SetupProvider(executionResult);

        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges).Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithCostSummary_StoresInPipeline()
    {
        var changes = new List<CodeChange>
        {
            new(new FilePath("file.cs"), "content", "Create")
        };
        var phases = new Dictionary<string, PhaseCost>
        {
            ["primary"] = new("claude-sonnet-4-20250514", 1000, 500, 200, 3, 0.05m)
        };
        var costSummary = new RunCostSummary(phases.AsReadOnly(), 0.05m);
        var executionResult = new AgentExecutionResult(changes, costSummary, 42);
        SetupProvider(executionResult);

        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        pipeline.TryGet<RunCostSummary>(ContextKeys.RunCostSummary, out var stored).Should().BeTrue();
        stored!.TotalCost.Should().Be(0.05m);
        pipeline.Get<int>(ContextKeys.RunDurationSeconds).Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_NoCostSummary_DoesNotStoreInPipeline()
    {
        var executionResult = new AgentExecutionResult(new List<CodeChange>(), null, null);
        SetupProvider(executionResult);

        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        pipeline.Has(ContextKeys.RunCostSummary).Should().BeFalse();
        pipeline.Has(ContextKeys.RunDurationSeconds).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_PropagatesException()
    {
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.ExecutePlanAsync(
                It.IsAny<Plan>(), It.IsAny<Repository>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<IProgressReporter?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Execution failed"));
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline);

        var act = async () => await _handler.ExecuteAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("Execution failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithDecisions_WritesViaDecisionLogger()
    {
        var changes = new List<CodeChange>();
        var decisions = new List<PlanDecision>
        {
            new("Architecture", "**Sealed classes**: prevents accidental inheritance"),
            new("TradeOff", "**No local Dynamics**: dev always against real environment")
        };
        var executionResult = new AgentExecutionResult(changes, null, null, decisions);
        SetupProvider(executionResult);

        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        _decisionLoggerMock.Verify(d => d.LogAsync(
            "/tmp", DecisionCategory.Architecture,
            "**Sealed classes**: prevents accidental inheritance",
            It.IsAny<CancellationToken>()), Times.Once);
        _decisionLoggerMock.Verify(d => d.LogAsync(
            "/tmp", DecisionCategory.TradeOff,
            "**No local Dynamics**: dev always against real environment",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithDecisions_StoresDecisionsInPipeline()
    {
        var decisions = new List<PlanDecision>
        {
            new("Architecture", "**Sealed**: reason"),
            new("TradeOff", "**No local**: reason")
        };
        var executionResult = new AgentExecutionResult(new List<CodeChange>(), null, null, decisions);
        SetupProvider(executionResult);

        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        pipeline.TryGet<List<PlanDecision>>(ContextKeys.Decisions, out var stored).Should().BeTrue();
        stored.Should().HaveCount(2);
    }

    private void SetupProvider(AgentExecutionResult executionResult)
    {
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.ExecutePlanAsync(
                It.IsAny<Plan>(), It.IsAny<Repository>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<IProgressReporter?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);
    }

    private static AgenticExecuteContext CreateContext(PipelineContext pipeline)
    {
        var plan = new Plan("Test", new List<PlanStep>(), "{}");
        var repo = new Repository("/tmp", new BranchName("main"), "https://github.com/org/repo.git");
        return new AgenticExecuteContext(
            plan, repo, "principles", new AgentConfig { Type = "claude" }, pipeline);
    }
}
