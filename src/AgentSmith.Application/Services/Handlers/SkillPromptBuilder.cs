using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds system and user prompts for skill round LLM calls.
/// Handles both discussion (multi-round) and structured (single-round) formats.
/// </summary>
public sealed class SkillPromptBuilder(
    PromptPrefixBuilder prefixBuilder,
    IPromptCatalog prompts,
    ISkillBodyResolver bodyResolver) : ISkillPromptBuilder
{
    private const string PlanPlaceholder = "{{plan}}";

    public (string SystemPrompt, string UserPrompt) BuildDiscussionPrompt(
        RoleSkillDefinition role, string domainSection,
        string? projectContext, string? domainRules, string? codeMap,
        IReadOnlyList<DiscussionEntry> discussionLog, int round,
        string? existingTests = null,
        SkillRole? assignedRole = null,
        PlanArtifact? planArtifact = null)
    {
        var (system, prefix, suffix) = BuildDiscussionPromptParts(
            role, domainSection, "", projectContext, domainRules, codeMap, discussionLog, round,
            existingTests, assignedRole, planArtifact);
        return (system, $"{prefix}\n\n{suffix}");
    }

    public (string SystemPrompt, string UserPrompt) BuildStructuredPrompt(
        RoleSkillDefinition role, string domainSection,
        string upstreamContext, string outputInstruction,
        string? existingTests = null,
        SkillRole? assignedRole = null,
        PlanArtifact? planArtifact = null)
    {
        var (system, prefix, suffix) = BuildStructuredPromptParts(
            role, domainSection, "", upstreamContext, outputInstruction,
            existingTests, assignedRole, planArtifact);
        return (system, $"{prefix}\n\n{suffix}");
    }

    public (string SystemPrompt, string UserPrefix, string UserSuffix) BuildDiscussionPromptParts(
        RoleSkillDefinition role, string domainStable, string domainVariable,
        string? projectContext, string? domainRules, string? codeMap,
        IReadOnlyList<DiscussionEntry> discussionLog, int round,
        string? existingTests = null,
        SkillRole? assignedRole = null,
        PlanArtifact? planArtifact = null)
    {
        var system = $"""
            {BuildRolePrompt(role, assignedRole, planArtifact)}

            {prompts.Get("observation-schema")}
            """;
        var discussionSoFar = RenderDiscussion(discussionLog);
        var (prefix, suffix) = prefixBuilder.BuildDiscussionParts(
            domainStable, domainVariable, projectContext, domainRules, codeMap, existingTests,
            discussionSoFar, round);
        return (system, prefix, suffix);
    }

    public (string SystemPrompt, string UserPrefix, string UserSuffix) BuildStructuredPromptParts(
        RoleSkillDefinition role, string domainStable, string domainVariable,
        string upstreamContext, string outputInstruction,
        string? existingTests = null,
        SkillRole? assignedRole = null,
        PlanArtifact? planArtifact = null)
    {
        var system = BuildRolePrompt(role, assignedRole, planArtifact);
        var (prefix, suffix) = prefixBuilder.BuildStructuredParts(
            domainStable, domainVariable, upstreamContext, outputInstruction, existingTests);
        return (system, prefix, suffix);
    }

    private static string RenderDiscussion(IReadOnlyList<DiscussionEntry> discussionLog) =>
        discussionLog.Count > 0
            ? string.Join("\n\n---\n\n", discussionLog.Select(e =>
                $"{e.Emoji} {e.DisplayName} (Round {e.Round}):\n{e.Content}"))
            : "No prior discussion.";

    private string BuildRolePrompt(
        RoleSkillDefinition role, SkillRole? assignedRole, PlanArtifact? planArtifact)
    {
        var body = assignedRole.HasValue
            ? bodyResolver.ResolveBody(role, assignedRole.Value)
            : role.Rules;
        if (body.Contains(PlanPlaceholder))
            body = body.Replace(PlanPlaceholder, RenderPlanArtifact(planArtifact));
        return $"""
            ## Your Role
            {role.DisplayName}: {role.Description}

            ## Role-Specific Rules
            {body}
            """;
    }

    private static string RenderPlanArtifact(PlanArtifact? artifact)
    {
        if (artifact is null || artifact.Observations.Count == 0)
            return "(no plan provided)";
        var lines = artifact.Observations.Select(o =>
            $"- [{o.Severity}] {o.Concern}: {o.Description}" +
            (string.IsNullOrWhiteSpace(o.Suggestion) ? "" : $"\n  → {o.Suggestion}"));
        return $"Plan from {artifact.LeadSkill ?? "lead"}:\n" + string.Join("\n", lines);
    }
}
