using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Shared skill round logic: role lookup, LLM call, discussion log, objection handling.
/// Subclasses provide the domain-specific user prompt section.
/// </summary>
public abstract class SkillRoundHandlerBase
{
    private static readonly Regex ObjectionPattern = new(
        @"OBJECTION\s*\[?\s*(\S+)\s*\]?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    protected abstract ILogger Logger { get; }
    protected abstract string BuildDomainSection(PipelineContext pipeline);
    protected virtual string SkillRoundCommandName => "SkillRoundCommand";

    protected async Task<CommandResult> ExecuteRoundAsync(
        string skillName, int round, PipelineContext pipeline,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
        {
            return CommandResult.Fail("No available roles in pipeline context");
        }

        var role = roles.FirstOrDefault(r => r.Name == skillName);
        if (role is null)
            return CommandResult.Fail($"Role '{skillName}' not found");

        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        pipeline.TryGet<string>(ContextKeys.DomainRules, out var domainRules);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);

        if (!pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var discussionLog) || discussionLog is null)
        {
            discussionLog = [];
        }

        var domainSection = BuildDomainSection(pipeline);
        var llmResponse = await CallLlmAsync(
            role, domainSection, projectContext, domainRules, codeMap,
            discussionLog, round, llmClient, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(llmResponse);

        var entry = new DiscussionEntry(
            skillName, role.DisplayName, role.Emoji,
            round, llmResponse.Text);
        discussionLog.Add(entry);
        pipeline.Set(ContextKeys.DiscussionLog, discussionLog);

        Logger.LogInformation(
            "{Emoji} {DisplayName} (Round {Round}): contributed to discussion",
            role.Emoji, role.DisplayName, round);

        var objectionMatch = ObjectionPattern.Match(llmResponse.Text);
        if (objectionMatch.Success)
        {
            var targetRole = objectionMatch.Groups[1].Value.Trim();
            var validTarget = roles.Any(r => r.Name == targetRole);

            if (validTarget)
            {
                var nextRound = round + 1;
                return CommandResult.OkAndContinueWith(
                    $"{role.DisplayName} objects, requesting response from {targetRole}",
                    PipelineCommand.SkillRound(SkillRoundCommandName, targetRole, nextRound),
                    PipelineCommand.SkillRound(SkillRoundCommandName, skillName, nextRound),
                    PipelineCommand.Simple(CommandNames.ConvergenceCheck));
            }
        }

        return CommandResult.Ok($"{role.DisplayName} (Round {round}): contributed");
    }

    private static async Task<LlmResponse> CallLlmAsync(
        RoleSkillDefinition role,
        string domainSection,
        string? projectContext,
        string? domainRules,
        string? codeMap,
        List<DiscussionEntry> discussionLog,
        int round,
        ILlmClient llmClient,
        CancellationToken cancellationToken)
    {
        var discussionSoFar = discussionLog.Count > 0
            ? string.Join("\n\n---\n\n", discussionLog.Select(e =>
                $"{e.Emoji} {e.DisplayName} (Round {e.Round}):\n{e.Content}"))
            : "No prior discussion.";

        var systemPrompt = $"""
            ## Your Role
            {role.DisplayName}: {role.Description}

            ## Role-Specific Rules
            {role.Rules}
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
            Based on the discussion so far, provide your analysis.
            This is round {round}.

            If this is the first round for the lead role: Create an initial analysis.
            If responding to an existing analysis: Review it from your perspective.

            At the end of your response, state clearly:
            - AGREE: if you accept the current analysis
            - OBJECTION [target_role]: if you have a blocking concern for a specific role
            - SUGGESTION: if you have a non-blocking improvement

            Keep your contribution focused and concise (max 500 words).
            """;

        return await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
    }
}
