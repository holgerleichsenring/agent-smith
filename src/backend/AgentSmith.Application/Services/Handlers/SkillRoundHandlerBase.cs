using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>Template-method router — picks discussion vs structured and delegates to injected executors. Subclasses inject an <see cref="ISkillPromptStrategy"/> + an <see cref="ISkillRoundToolPolicy"/>.</summary>
public abstract class SkillRoundHandlerBase(
    IDiscussionRoundExecutor discussionExecutor,
    IStructuredRoundExecutor structuredExecutor)
{
    protected abstract ILogger Logger { get; }
    protected abstract ISkillPromptStrategy Strategy { get; }
    protected abstract ISkillRoundToolPolicy ToolPolicy { get; }

    protected async Task<CommandResult> ExecuteRoundAsync(
        string skillName, int round, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        // p0199d: presets that wire Triage → SkillRound place a parameterless
        // SkillRoundCommand in the static chain as a marker. Triage's
        // PhaseCommandExpander inserts the actual per-skill SkillRound rounds
        // AFTER the Triage step; the original marker still runs but carries
        // no SkillName. Treat that as a deterministic no-op so the preset
        // doesn't crash on the leftover placeholder.
        if (string.IsNullOrEmpty(skillName))
            return CommandResult.Ok("SkillRound: no SkillName on marker command — Triage handled dispatch");
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
            return CommandResult.Fail("No available roles in pipeline context");
        var role = roles.FirstOrDefault(r => r.Name == skillName);
        if (role is null) return CommandResult.Fail($"Role '{skillName}' not found");
        pipeline.Set(ContextKeys.ActiveSkill, skillName);
        return IsStructuredRound(role, pipeline)
            ? await structuredExecutor.ExecuteAsync(skillName, role, Strategy, ToolPolicy, pipeline, Logger, cancellationToken)
            : await discussionExecutor.ExecuteAsync(skillName, role, roles, round, Strategy, ToolPolicy, pipeline, Logger, cancellationToken);
    }

    private static bool IsStructuredRound(RoleSkillDefinition role, PipelineContext pipeline) =>
        role.Orchestration is not null
        && pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var pipelineType)
        && pipelineType is not PipelineType.Discussion;
}
