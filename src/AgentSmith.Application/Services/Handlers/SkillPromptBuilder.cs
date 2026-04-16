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

        var systemPrompt = $"""
            {BuildRolePrompt(role)}

            {ObservationSchemaInstruction}
            """;

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
            Based on the discussion so far, provide your analysis as a JSON array of observations.
            This is round {round}.

            Respond ONLY with a JSON array. No other text.
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
