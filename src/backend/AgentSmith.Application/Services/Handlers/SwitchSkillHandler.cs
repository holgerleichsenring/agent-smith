using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Swaps the active domain rules and skill type in PipelineContext.
/// Merges base role rules with project-level extra_rules.
/// </summary>
public sealed class SwitchSkillHandler(
    ILogger<SwitchSkillHandler> logger)
    : ICommandHandler<SwitchSkillContext>
{
    public Task<CommandResult> ExecuteAsync(
        SwitchSkillContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
        {
            return Task.FromResult(CommandResult.Fail(
                "No available roles in pipeline context. Skill switching requires loaded roles."));
        }

        var role = roles.FirstOrDefault(r => r.Name == context.SkillName)
                   ?? throw new ConfigurationException(
                       $"Role '{context.SkillName}' not found in available roles.");

        context.Pipeline.Set(ContextKeys.DomainRules, role.Rules);
        context.Pipeline.Set(ContextKeys.ActiveSkill, context.SkillName);

        logger.LogInformation(
            "Switched to skill: {Emoji} {DisplayName}",
            role.Emoji, role.DisplayName);

        return Task.FromResult(CommandResult.Ok(
            $"Switched to {role.DisplayName} perspective"));
    }
}
