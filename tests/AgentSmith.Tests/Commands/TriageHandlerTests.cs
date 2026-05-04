using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class TriageHandlerTests
{
    private readonly Mock<ILlmClientFactory> _llmFactoryMock = new();
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly TriageHandler _handler;

    public TriageHandlerTests()
    {
        _llmFactoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(_llmClientMock.Object);
        var legacyStrategy = new LegacyTriageStrategy(
            new FakePromptCatalog(), NullLogger<LegacyTriageStrategy>.Instance);
        var selectorMock = new Mock<ITriageStrategySelector>();
        selectorMock.Setup(s => s.Select(It.IsAny<PipelineType>())).Returns(legacyStrategy);
        _handler = new TriageHandler(
            _llmFactoryMock.Object,
            selectorMock.Object,
            NullLogger<TriageHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoRolesAvailable_SkipsTriage()
    {
        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No roles available");
    }

    [Fact]
    public async Task ExecuteAsync_SingleParticipant_SkipsDiscussion()
    {
        var pipeline = CreatePipelineWithRolesAndTicket();
        var context = CreateContext(pipeline);

        _llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("""{"lead": "architect", "participants": ["architect"]}""", 0, 0));

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("single role");
        result.InsertNext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleParticipants_InsertsSkillRoundsAndConvergence()
    {
        var pipeline = CreatePipelineWithRolesAndTicket();
        var context = CreateContext(pipeline);

        _llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("""{"lead": "architect", "participants": ["architect", "tester"]}""", 0, 0));

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().NotBeNull();
        result.InsertNext!.Select(c => c.DisplayName).Should().Contain("SkillRoundCommand:architect:1");
        result.InsertNext.Select(c => c.DisplayName).Should().Contain("SkillRoundCommand:tester:1");
        result.InsertNext.Select(c => c.DisplayName).Should().Contain("ConvergenceCheckCommand");
    }

    [Fact]
    public async Task ExecuteAsync_LeadGoesFirst()
    {
        var pipeline = CreatePipelineWithRolesAndTicket();
        var context = CreateContext(pipeline);

        _llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("""{"lead": "tester", "participants": ["architect", "tester"]}""", 0, 0));

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.InsertNext![0].DisplayName.Should().Be("SkillRoundCommand:tester:1");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidParticipants_Filtered()
    {
        var pipeline = CreatePipelineWithRolesAndTicket();
        var context = CreateContext(pipeline);

        _llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("""{"lead": "architect", "participants": ["architect", "nonexistent"]}""", 0, 0));

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("single role");
        result.InsertNext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_LlmFails_SkipsDiscussion()
    {
        var pipeline = CreatePipelineWithRolesAndTicket();
        var context = CreateContext(pipeline);

        _llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM timeout"));

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("no roles needed");
    }

    [Fact]
    public async Task ExecuteAsync_MalformedJson_SkipsDiscussion()
    {
        var pipeline = CreatePipelineWithRolesAndTicket();
        var context = CreateContext(pipeline);

        _llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("this is not json at all", 0, 0));

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("no roles needed");
    }

    private static TriageContext CreateContext(PipelineContext pipeline)
    {
        return new TriageContext(
            new AgentConfig { Type = "claude" },
            pipeline);
    }

    private static PipelineContext CreatePipelineWithRolesAndTicket()
    {
        var pipeline = new PipelineContext();
        var ticket = new Ticket(
            new TicketId("42"), "Fix performance",
            "The API is slow", null, "Open", "github");
        pipeline.Set(ContextKeys.Ticket, ticket);
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, new List<RoleSkillDefinition>
        {
            new()
            {
                Name = "architect", DisplayName = "Architect", Emoji = "🏗️",
                Description = "System architecture", Triggers = ["architecture"],
                Rules = "Design rules"
            },
            new()
            {
                Name = "tester", DisplayName = "Tester", Emoji = "🧪",
                Description = "Quality assurance", Triggers = ["test"],
                Rules = "Test rules"
            }
        });
        return pipeline;
    }
}
