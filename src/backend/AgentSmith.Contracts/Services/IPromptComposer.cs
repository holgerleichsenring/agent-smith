using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0147d: Composes the full chat-prompt triple (system / user-prefix /
/// user-suffix) for a skill round. Wraps <see cref="ISkillPromptBuilder"/> with
/// the pipeline-context resolution (project context, domain rules, code map,
/// discussion log, plan artifact, existing tests, etc.) so the calling handler
/// only supplies the role + round + per-round <see cref="ISkillPromptStrategy"/>.
/// </summary>
public interface IPromptComposer
{
    (string SystemPrompt, string UserPrefix, string UserSuffix) ComposeDiscussion(
        RoleSkillDefinition role,
        ISkillPromptStrategy strategy,
        string skillName,
        int round,
        PipelineContext pipeline);

    (string SystemPrompt, string UserPrefix, string UserSuffix) ComposeStructured(
        RoleSkillDefinition role,
        ISkillPromptStrategy strategy,
        PipelineContext pipeline);
}
