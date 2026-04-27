using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Builds system and user prompts for skill round LLM calls.
/// </summary>
public interface ISkillPromptBuilder
{
    (string SystemPrompt, string UserPrompt) BuildDiscussionPrompt(
        RoleSkillDefinition role,
        string domainSection,
        string? projectContext,
        string? domainRules,
        string? codeMap,
        IReadOnlyList<DiscussionEntry> discussionLog,
        int round);

    (string SystemPrompt, string UserPrompt) BuildStructuredPrompt(
        RoleSkillDefinition role,
        string domainSection,
        string upstreamContext,
        string outputInstruction);

    /// <summary>
    /// Cache-aware variant. Returns (system, userPrefix, userSuffix) so the prefix
    /// can be sent under cache_control while the suffix varies per skill.
    /// </summary>
    (string SystemPrompt, string UserPrefix, string UserSuffix) BuildDiscussionPromptParts(
        RoleSkillDefinition role,
        string domainStable,
        string domainVariable,
        string? projectContext,
        string? domainRules,
        string? codeMap,
        IReadOnlyList<DiscussionEntry> discussionLog,
        int round);

    (string SystemPrompt, string UserPrefix, string UserSuffix) BuildStructuredPromptParts(
        RoleSkillDefinition role,
        string domainStable,
        string domainVariable,
        string upstreamContext,
        string outputInstruction);
}
