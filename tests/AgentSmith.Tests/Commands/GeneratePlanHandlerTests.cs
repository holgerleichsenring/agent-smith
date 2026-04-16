using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
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
    public async Task ExecuteAsync_ConsolidatedPlanExists_PassesAsContextToProvider()
    {
        var plan = new Plan("Structured plan", new List<PlanStep>(), "{}");
        var providerMock = new Mock<IAgentProvider>();
        string? capturedContext = null;
        providerMock.Setup(p => p.GeneratePlanAsync(
                It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CodeAnalysis, string, string?, string?, CancellationToken>(
                (_, _, _, _, ctx, _) => capturedContext = ctx)
            .ReturnsAsync(plan);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ConsolidatedPlan, "Architecture: use Strategy Pattern");

        var context = new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline,
            ProjectContext: "existing context");

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedContext.Should().Contain("existing context");
        capturedContext.Should().Contain("Multi-Role Discussion");
        capturedContext.Should().Contain("Architecture: use Strategy Pattern");
        pipeline.TryGet<Plan>(ContextKeys.Plan, out _).Should().BeTrue();
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
            It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        _decisionLoggerMock.Verify(d => d.LogAsync(
            "/tmp/repo", DecisionCategory.Tooling,
            "**DuckDB**: reads Parquet natively",
            It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConvergenceResultPresent_MapsBlockingToSteps()
    {
        var plan = new Plan("Structured plan", new List<PlanStep>
        {
            new(1, "Fix SQL injection", null, "modify"),
            new(2, "Add eager loading", null, "modify")
        }, "{}");
        string? capturedContext = null;
        var providerMock = SetupProvider(plan, (_, _, _, _, ctx, _) => capturedContext = ctx);

        var pipeline = new PipelineContext();
        var convergence = new ConvergenceResult(
            true,
            new List<SkillObservation>
            {
                new(1, "security", ObservationConcern.Security, "SQL injection", "Use parameterized queries", true, ObservationSeverity.High, 95, Location: "src/Data/Repo.cs:42", Effort: ObservationEffort.Small),
                new(2, "performance", ObservationConcern.Performance, "N+1 query", "Add eager loading", false, ObservationSeverity.Medium, 80)
            },
            new List<ObservationLink>(),
            new List<string>(),
            new List<SkillObservation>
            {
                new(1, "security", ObservationConcern.Security, "SQL injection", "Use parameterized queries", true, ObservationSeverity.High, 95, Location: "src/Data/Repo.cs:42", Effort: ObservationEffort.Small)
            },
            new List<SkillObservation>
            {
                new(2, "performance", ObservationConcern.Performance, "N+1 query", "Add eager loading", false, ObservationSeverity.Medium, 80)
            });
        pipeline.Set(ContextKeys.ConvergenceResult, convergence);

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedContext.Should().Contain("Blocking Observations");
        capturedContext.Should().Contain("SQL injection");
        capturedContext.Should().Contain("Use parameterized queries");
        capturedContext.Should().Contain("src/Data/Repo.cs:42");
    }

    [Fact]
    public async Task ExecuteAsync_ConvergenceResultPresent_NeverSkips()
    {
        var plan = new Plan("Plan", new List<PlanStep>(), "{}");
        var providerMock = SetupProvider(plan);

        var pipeline = new PipelineContext();
        var convergence = new ConvergenceResult(
            true,
            new List<SkillObservation>(),
            new List<ObservationLink>(),
            new List<string>(),
            new List<SkillObservation>(),
            new List<SkillObservation>());
        pipeline.Set(ContextKeys.ConvergenceResult, convergence);

        var context = CreateContext(pipeline);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Has(ContextKeys.Plan).Should().BeTrue();
        providerMock.Verify(p => p.GeneratePlanAsync(
            It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConvergenceResult_SuggestionMapsToStepAction()
    {
        var plan = new Plan("Plan", new List<PlanStep>(), "{}");
        string? capturedContext = null;
        SetupProvider(plan, (_, _, _, _, ctx, _) => capturedContext = ctx);

        var pipeline = new PipelineContext();
        var blocking = new SkillObservation(
            1, "architect", ObservationConcern.Architecture,
            "Missing abstraction layer",
            "Extract IRepository interface",
            true, ObservationSeverity.High, 90);
        var convergence = new ConvergenceResult(
            true,
            new List<SkillObservation> { blocking },
            new List<ObservationLink>(),
            new List<string>(),
            new List<SkillObservation> { blocking },
            new List<SkillObservation>());
        pipeline.Set(ContextKeys.ConvergenceResult, convergence);

        var context = CreateContext(pipeline);
        await _handler.ExecuteAsync(context, CancellationToken.None);

        capturedContext.Should().Contain("Action: Extract IRepository interface");
    }

    [Fact]
    public async Task ExecuteAsync_ConvergenceResult_LocationMapsToTargetFile()
    {
        var plan = new Plan("Plan", new List<PlanStep>(), "{}");
        string? capturedContext = null;
        SetupProvider(plan, (_, _, _, _, ctx, _) => capturedContext = ctx);

        var pipeline = new PipelineContext();
        var blocking = new SkillObservation(
            1, "security", ObservationConcern.Security,
            "Hardcoded secret",
            "Move to config",
            true, ObservationSeverity.High, 95,
            Location: "src/Config/Secrets.cs:15");
        var convergence = new ConvergenceResult(
            true,
            new List<SkillObservation> { blocking },
            new List<ObservationLink>(),
            new List<string>(),
            new List<SkillObservation> { blocking },
            new List<SkillObservation>());
        pipeline.Set(ContextKeys.ConvergenceResult, convergence);

        var context = CreateContext(pipeline);
        await _handler.ExecuteAsync(context, CancellationToken.None);

        capturedContext.Should().Contain("target: src/Config/Secrets.cs:15");
    }

    private Mock<IAgentProvider> SetupProvider(
        Plan plan,
        Action<Ticket, CodeAnalysis, string, string?, string?, CancellationToken>? callback = null)
    {
        var providerMock = new Mock<IAgentProvider>();
        var setup = providerMock.Setup(p => p.GeneratePlanAsync(
            It.IsAny<Ticket>(), It.IsAny<CodeAnalysis>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()));

        if (callback is not null)
            setup.Callback(callback);

        setup.ReturnsAsync(plan);

        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        return providerMock;
    }

    private static GeneratePlanContext CreateContext(PipelineContext pipeline)
    {
        var ticket = new Ticket(new TicketId("1"), "Title", "Desc", null, "Open", "github");
        var codeAnalysis = new CodeAnalysis(new List<string>(), new List<string>(), "dotnet", "C#");
        return new GeneratePlanContext(
            ticket, codeAnalysis, "principles", new AgentConfig { Type = "claude" }, pipeline);
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
            It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
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
