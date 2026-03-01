using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public sealed class SwitchSkillHandlerTests
{
    private readonly SwitchSkillHandler _handler = new(
        NullLogger<SwitchSkillHandler>.Instance);

    [Fact]
    public async Task ExecuteAsync_ValidRole_SwitchesDomainRulesAndActiveSkill()
    {
        var pipeline = new PipelineContext();
        var roles = CreateRoles();
        pipeline.Set(ContextKeys.AvailableRoles, roles);

        var context = new SwitchSkillContext("architect", pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Architect");
        pipeline.Get<string>(ContextKeys.DomainRules).Should().Contain("architecture");
        pipeline.Get<string>(ContextKeys.ActiveSkill).Should().Be("architect");
    }

    [Fact]
    public async Task ExecuteAsync_NoRolesInPipeline_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var context = new SwitchSkillContext("architect", pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("No available roles");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownRole_ThrowsConfigurationException()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.AvailableRoles, CreateRoles());

        var context = new SwitchSkillContext("unknown-role", pipeline);

        var act = async () => await _handler.ExecuteAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*unknown-role*not found*");
    }

    [Fact]
    public async Task ExecuteAsync_SecondSwitch_OverridesPreviousSkill()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.AvailableRoles, CreateRoles());

        await _handler.ExecuteAsync(
            new SwitchSkillContext("architect", pipeline), CancellationToken.None);
        await _handler.ExecuteAsync(
            new SwitchSkillContext("tester", pipeline), CancellationToken.None);

        pipeline.Get<string>(ContextKeys.ActiveSkill).Should().Be("tester");
        pipeline.Get<string>(ContextKeys.DomainRules).Should().Contain("testing");
    }

    private static IReadOnlyList<RoleSkillDefinition> CreateRoles() =>
    [
        new RoleSkillDefinition
        {
            Name = "architect",
            DisplayName = "Architect",
            Emoji = "🏗️",
            Description = "System architecture",
            Rules = "Focus on architecture patterns",
            Triggers = ["architecture", "design"]
        },
        new RoleSkillDefinition
        {
            Name = "tester",
            DisplayName = "Tester",
            Emoji = "🧪",
            Description = "Quality assurance",
            Rules = "Focus on testing strategies",
            Triggers = ["test", "qa"]
        }
    ];
}
