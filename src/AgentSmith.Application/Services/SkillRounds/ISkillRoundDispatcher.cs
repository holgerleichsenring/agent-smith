using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Single skill-call invocation point for skill rounds. Builds the
/// <see cref="SkillCallRequest"/> from the composed prompt triple + role,
/// invokes <c>ISkillCallRuntime</c> under the pipeline cost-tracker scope, and
/// returns the raw <see cref="SkillCallResult"/>. Outcome → CommandResult
/// translation is the caller's concern (see SkillCallOutcomeTranslator).
/// </summary>
public interface ISkillRoundDispatcher
{
    Task<SkillCallResult> DispatchAsync(
        string skillName,
        RoleSkillDefinition role,
        string systemPrompt,
        string userPrefix,
        string userSuffix,
        PipelineContext pipeline,
        CancellationToken cancellationToken);
}
