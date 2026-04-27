using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds system and user prompts for skill round LLM calls.
/// Handles both discussion (multi-round) and structured (single-round) formats.
/// </summary>
public sealed class SkillPromptBuilder(PromptPrefixBuilder prefixBuilder) : ISkillPromptBuilder
{
    internal const string ObservationSchemaInstruction = """
        ## Output Format — SkillObservation

        You MUST respond with ONLY a JSON array of observations. No preamble, no markdown fences, no explanation outside the JSON.

        Each observation has this shape:
        {
          "concern": "correctness" | "architecture" | "performance" | "security" | "legal" | "compliance" | "risk",
          "description": "What you observed — the problem or insight",
          "suggestion": "What should be done about it",
          "blocking": true/false,
          "severity": "high" | "medium" | "low" | "info",
          "confidence": 0-100,
          "rationale": "Why you believe this (optional)",
          "location": "File:Line or API path (optional)",
          "effort": "small" | "medium" | "large" (optional)
        }

        Rules:
        - Do NOT include an "id" field — IDs are assigned by the framework.
        - "blocking" = true means this MUST be addressed before proceeding.
        - "confidence" reflects how certain you are (0 = guess, 100 = certain).
        - Produce 1–5 observations. Prefer fewer, higher-quality observations over many weak ones.

        Example:
        [
          {
            "concern": "security",
            "description": "The /api/auth/login endpoint accepts passwords in query parameters.",
            "suggestion": "Move password to POST body.",
            "blocking": true,
            "severity": "high",
            "confidence": 95,
            "location": "POST /api/auth/login",
            "effort": "small"
          }
        ]
        """;

    public (string SystemPrompt, string UserPrompt) BuildDiscussionPrompt(
        RoleSkillDefinition role, string domainSection,
        string? projectContext, string? domainRules, string? codeMap,
        IReadOnlyList<DiscussionEntry> discussionLog, int round)
    {
        var (system, prefix, suffix) = BuildDiscussionPromptParts(
            role, domainSection, "", projectContext, domainRules, codeMap, discussionLog, round);
        return (system, $"{prefix}\n\n{suffix}");
    }

    public (string SystemPrompt, string UserPrompt) BuildStructuredPrompt(
        RoleSkillDefinition role, string domainSection,
        string upstreamContext, string outputInstruction)
    {
        var (system, prefix, suffix) = BuildStructuredPromptParts(
            role, domainSection, "", upstreamContext, outputInstruction);
        return (system, $"{prefix}\n\n{suffix}");
    }

    public (string SystemPrompt, string UserPrefix, string UserSuffix) BuildDiscussionPromptParts(
        RoleSkillDefinition role, string domainStable, string domainVariable,
        string? projectContext, string? domainRules, string? codeMap,
        IReadOnlyList<DiscussionEntry> discussionLog, int round)
    {
        var system = $"""
            {BuildRolePrompt(role)}

            {ObservationSchemaInstruction}
            """;
        var discussionSoFar = RenderDiscussion(discussionLog);
        var (prefix, suffix) = prefixBuilder.BuildDiscussionParts(
            domainStable, domainVariable, projectContext, domainRules, codeMap,
            discussionSoFar, round);
        return (system, prefix, suffix);
    }

    public (string SystemPrompt, string UserPrefix, string UserSuffix) BuildStructuredPromptParts(
        RoleSkillDefinition role, string domainStable, string domainVariable,
        string upstreamContext, string outputInstruction)
    {
        var system = BuildRolePrompt(role);
        var (prefix, suffix) = prefixBuilder.BuildStructuredParts(
            domainStable, domainVariable, upstreamContext, outputInstruction);
        return (system, prefix, suffix);
    }

    private static string RenderDiscussion(IReadOnlyList<DiscussionEntry> discussionLog) =>
        discussionLog.Count > 0
            ? string.Join("\n\n---\n\n", discussionLog.Select(e =>
                $"{e.Emoji} {e.DisplayName} (Round {e.Round}):\n{e.Content}"))
            : "No prior discussion.";

    private static string BuildRolePrompt(RoleSkillDefinition role) => $"""
        ## Your Role
        {role.DisplayName}: {role.Description}

        ## Role-Specific Rules
        {role.Rules}
        """;
}
