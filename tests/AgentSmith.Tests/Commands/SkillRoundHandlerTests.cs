using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class SkillRoundHandlerTests
{
    private readonly Mock<ILlmClientFactory> _llmFactoryMock = new();
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly SkillRoundHandler _handler;

    public SkillRoundHandlerTests()
    {
        _llmFactoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(_llmClientMock.Object);
        _handler = new SkillRoundHandler(
            _llmFactoryMock.Object,
            new SkillPromptBuilder(),
            new GateOutputHandler(NullLogger<GateOutputHandler>.Instance),
            new UpstreamContextBuilder(),
            NullLogger<SkillRoundHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoRoles_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, CreateTicket());
        var context = new SkillRoundContext("architect", 1, new AgentConfig(), pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("No available roles");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownRole_ReturnsFail()
    {
        var pipeline = CreatePipeline();
        var context = new SkillRoundContext("unknown", 1, new AgentConfig(), pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_Agree_ReturnsOkWithoutInsertion()
    {
        var pipeline = CreatePipeline();
        SetupLlmResponse("I reviewed the plan and I AGREE with the approach.");

        var context = new SkillRoundContext("architect", 1, new AgentConfig(), pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().BeNull();
        result.Message.Should().Contain("Architect");
    }

    [Fact]
    public async Task ExecuteAsync_Objection_InsertsFollowUpCommands()
    {
        var pipeline = CreatePipeline();
        SetupLlmResponse("This needs more testing. OBJECTION tester");

        var context = new SkillRoundContext("architect", 1, new AgentConfig(), pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().NotBeNull();
        result.InsertNext.Should().HaveCount(3);
        result.InsertNext![0].DisplayName.Should().Be("SkillRoundCommand:tester:2");
        result.InsertNext[1].DisplayName.Should().Be("SkillRoundCommand:architect:2");
        result.InsertNext[2].DisplayName.Should().Be("ConvergenceCheckCommand");
    }

    [Fact]
    public async Task ExecuteAsync_ObjectionInvalidTarget_NoInsertion()
    {
        var pipeline = CreatePipeline();
        SetupLlmResponse("This is bad. OBJECTION nonexistent");

        var context = new SkillRoundContext("architect", 1, new AgentConfig(), pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Suggestion_NoInsertion()
    {
        var pipeline = CreatePipeline();
        SetupLlmResponse("Consider adding caching. SUGGESTION");

        var context = new SkillRoundContext("architect", 1, new AgentConfig(), pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_AppendsToDiscussionLog()
    {
        var pipeline = CreatePipeline();
        SetupLlmResponse("My initial plan. AGREE");

        var context = new SkillRoundContext("architect", 1, new AgentConfig(), pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        var log = pipeline.Get<List<DiscussionEntry>>(ContextKeys.DiscussionLog);
        log.Should().HaveCount(1);
        log[0].RoleName.Should().Be("architect");
        log[0].DisplayName.Should().Be("Architect");
        log[0].Round.Should().Be(1);
        log[0].Content.Should().Contain("My initial plan");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesExistingDiscussionLog()
    {
        var pipeline = CreatePipeline();
        var existingLog = new List<DiscussionEntry>
        {
            new("architect", "Architect", "🏗️", 1, "Initial plan")
        };
        pipeline.Set(ContextKeys.DiscussionLog, existingLog);

        SetupLlmResponse("I reviewed the plan. AGREE");

        var context = new SkillRoundContext("tester", 1, new AgentConfig(), pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        var log = pipeline.Get<List<DiscussionEntry>>(ContextKeys.DiscussionLog);
        log.Should().HaveCount(2);
        log[0].RoleName.Should().Be("architect");
        log[1].RoleName.Should().Be("tester");
    }

    private void SetupLlmResponse(string response)
    {
        _llmClientMock.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(response, 0, 0));
    }

    private static PipelineContext CreatePipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, CreateTicket());
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles,
            new List<RoleSkillDefinition>
            {
                new()
                {
                    Name = "architect", DisplayName = "Architect", Emoji = "🏗️",
                    Description = "System architecture", Triggers = ["architecture"],
                    Rules = "Architecture rules"
                },
                new()
                {
                    Name = "tester", DisplayName = "Tester", Emoji = "🧪",
                    Description = "Quality assurance", Triggers = ["test"],
                    Rules = "Testing rules"
                }
            });
        return pipeline;
    }

    private static Ticket CreateTicket() =>
        new(new TicketId("42"), "Fix bug", "Fix the login bug", null, "Open", "github");
}
