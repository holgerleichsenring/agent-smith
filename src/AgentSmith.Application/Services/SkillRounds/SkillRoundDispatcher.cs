using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Builds the <see cref="SkillCallRequest"/> from the composed prompt
/// triple + role, then invokes <see cref="ISkillCallRuntime"/> under the
/// pipeline cost-tracker scope. Single skill-call invocation point shared by
/// discussion + structured rounds (gate rounds bypass this — they own their
/// own retry-coordinator policy per p0142).
/// </summary>
public sealed class SkillRoundDispatcher(ISkillCallRuntime skillCallRuntime) : ISkillRoundDispatcher
{
    public async Task<SkillCallResult> DispatchAsync(
        string skillName, RoleSkillDefinition role,
        string systemPrompt, string userPrefix, string userSuffix,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var combinedUser = string.IsNullOrEmpty(userSuffix) ? userPrefix : $"{userPrefix}\n\n{userSuffix}";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, combinedUser),
        };
        var request = new SkillCallRequest
        {
            SkillName = skillName,
            Role = role.Role ?? "investigator",
            Phase = MapPhase(pipeline),
            PromptParts = messages,
            ToolSet = Array.Empty<AITool>(),
            AgentConfig = pipeline.Get<AgentConfig>(ContextKeys.AgentConfig),
            TaskType = TaskType.Primary,
            PipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null,
        };
        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);
        return await skillCallRuntime.ExecuteAsync(request, costTracker, cancellationToken);
    }

    private static SkillExecutionPhase MapPhase(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<PipelinePhase>(ContextKeys.CurrentPhase, out var phase))
            return SkillExecutionPhase.Discuss;
        return phase switch
        {
            PipelinePhase.Plan => SkillExecutionPhase.Plan,
            PipelinePhase.Review => SkillExecutionPhase.Review,
            PipelinePhase.Final => SkillExecutionPhase.Synthesize,
            _ => SkillExecutionPhase.Discuss,
        };
    }
}
