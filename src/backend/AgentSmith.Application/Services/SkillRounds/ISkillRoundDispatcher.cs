using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// Single skill-call invocation point for skill rounds. Builds the
/// <see cref="SkillCallRequest"/> from the composed prompt triple + role,
/// invokes <c>ISkillCallRuntime</c> under the pipeline cost-tracker scope, and
/// returns the raw <see cref="SkillCallResult"/>. The tool set is resolved per
/// call from the supplied <see cref="ISkillRoundToolPolicy"/> so each round
/// handler can opt a different tool surface in. Outcome → CommandResult
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
        ISkillRoundToolPolicy toolPolicy,
        PipelineContext pipeline,
        CancellationToken cancellationToken);
}
