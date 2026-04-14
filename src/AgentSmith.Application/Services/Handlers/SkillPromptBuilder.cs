using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds system and user prompts for skill round LLM calls.
/// Handles both discussion (multi-round) and structured (single-round) formats.
/// </summary>
public sealed class SkillPromptBuilder : ISkillPromptBuilder
{
    public (string SystemPrompt, string UserPrompt) BuildDiscussionPrompt(
        RoleSkillDefinition role,
        string domainSection,
        string? projectContext,
        string? domainRules,
        string? codeMap,
        IReadOnlyList<DiscussionEntry> discussionLog,
        int round)
    {
        var discussionSoFar = discussionLog.Count > 0
            ? string.Join("\n\n---\n\n", discussionLog.Select(e =>
                $"{e.Emoji} {e.DisplayName} (Round {e.Round}):\n{e.Content}"))
            : "No prior discussion.";

        var systemPrompt = BuildRolePrompt(role);

        var userPrompt = $"""
            {domainSection}

            ## Project Context
            {projectContext ?? "Not available"}

            ## Domain Rules
            {domainRules ?? "Not available"}

            ## Code Map
            {codeMap ?? "Not available"}

            ## Discussion So Far
            {discussionSoFar}

            ## Your Task
            Based on the discussion so far, provide your analysis.
            This is round {round}.

            At the end of your response, state clearly:
            - AGREE: if you accept the current analysis
            - OBJECTION [target_role]: if you have a blocking concern for a specific role
            - SUGGESTION: if you have a non-blocking improvement

            Keep your contribution focused and concise (max 500 words).
            """;

        return (systemPrompt, userPrompt);
    }

    public (string SystemPrompt, string UserPrompt) BuildStructuredPrompt(
        RoleSkillDefinition role,
        string domainSection,
        string upstreamContext,
        string outputInstruction)
    {
        var systemPrompt = BuildRolePrompt(role);

        var userPrompt = $"""
            {domainSection}

            {(string.IsNullOrEmpty(upstreamContext) ? "" : $"## Upstream Analysis\n{upstreamContext}\n")}

            ## Output Format
            {outputInstruction}
            """;

        return (systemPrompt, userPrompt);
    }

    private static string BuildRolePrompt(RoleSkillDefinition role) => $"""
        ## Your Role
        {role.DisplayName}: {role.Description}

        ## Role-Specific Rules
        {role.Rules}
        """;
}
