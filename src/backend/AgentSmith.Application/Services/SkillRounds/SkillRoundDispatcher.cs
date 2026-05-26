using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// Builds the <see cref="SkillCallRequest"/> from the composed prompt triple +
/// role, resolves the tool set from the caller-supplied
/// <see cref="ISkillRoundToolPolicy"/>, then invokes <see cref="ISkillCallRuntime"/>
/// under the pipeline cost-tracker scope. Emits the
/// <c>tool_set_size</c> concept so operators can see at-a-glance which rounds
/// had real tool access vs which ran single-shot.
/// </summary>
public sealed class SkillRoundDispatcher(
    ISkillCallRuntime skillCallRuntime,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<SkillRoundDispatcher> logger) : ISkillRoundDispatcher, IConceptWriter
{
    public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; } =
        [new ConceptDeclaration("tool_set_size", ConceptType.Int)];

    public async Task<SkillCallResult> DispatchAsync(
        string skillName, RoleSkillDefinition role,
        string systemPrompt, string userPrefix, string userSuffix,
        ISkillRoundToolPolicy toolPolicy,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var combinedUser = string.IsNullOrEmpty(userSuffix) ? userPrefix : $"{userPrefix}\n\n{userSuffix}";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, combinedUser),
        };
        var tools = toolPolicy.GetTools(role, pipeline);
        EmitToolSetSize(pipeline, tools.Count, skillName);
        var request = new SkillCallRequest
        {
            SkillName = skillName,
            Role = role.Role ?? "investigator",
            Phase = MapPhase(pipeline),
            InvestigatorMode = role.InvestigatorMode,
            PromptParts = messages,
            ToolSet = tools,
            AgentConfig = pipeline.Get<AgentConfig>(ContextKeys.AgentConfig),
            TaskType = TaskType.Primary,
            PipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null,
        };
        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);
        return await skillCallRuntime.ExecuteAsync(request, costTracker, cancellationToken);
    }

    private void EmitToolSetSize(PipelineContext pipeline, int size, string skillName)
    {
        try
        {
            conceptsFactory(pipeline).SetInt("tool_set_size", size);
        }
        catch (KeyNotFoundException)
        {
            logger.LogDebug(
                "tool_set_size concept undeclared in vocabulary; skipping emit for skill {Skill}", skillName);
        }
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
