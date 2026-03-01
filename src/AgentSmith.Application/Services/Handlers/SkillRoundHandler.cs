using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Generates a role-specific planning contribution for the discussion.
/// Appends response to the discussion log. Inserts follow-up commands on OBJECTION.
/// </summary>
public sealed class SkillRoundHandler(
    ILlmClientFactory llmClientFactory,
    ILogger<SkillRoundHandler> logger)
    : ICommandHandler<SkillRoundContext>
{
    private static readonly Regex ObjectionPattern = new(
        @"OBJECTION\s*\[?\s*(\S+)\s*\]?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<CommandResult> ExecuteAsync(
        SkillRoundContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
        {
            return CommandResult.Fail("No available roles in pipeline context");
        }

        var role = roles.FirstOrDefault(r => r.Name == context.SkillName);
        if (role is null)
            return CommandResult.Fail($"Role '{context.SkillName}' not found");

        var ticket = context.Pipeline.Get<Ticket>(ContextKeys.Ticket);
        context.Pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        context.Pipeline.TryGet<string>(ContextKeys.DomainRules, out var domainRules);
        context.Pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);

        // Get existing discussion log
        if (!context.Pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var discussionLog) || discussionLog is null)
        {
            discussionLog = [];
        }

        var llmClient = llmClientFactory.Create(context.AgentConfig);
        var response = await CallSkillRoundLlmAsync(
            role, ticket, projectContext, domainRules, codeMap,
            discussionLog, context.Round, llmClient, cancellationToken);

        // Append to discussion log
        var entry = new DiscussionEntry(
            context.SkillName, role.DisplayName, role.Emoji,
            context.Round, response);
        discussionLog.Add(entry);
        context.Pipeline.Set(ContextKeys.DiscussionLog, discussionLog);

        logger.LogInformation(
            "{Emoji} {DisplayName} (Round {Round}): contributed to discussion",
            role.Emoji, role.DisplayName, context.Round);

        // Check for OBJECTION
        var objectionMatch = ObjectionPattern.Match(response);
        if (objectionMatch.Success)
        {
            var targetRole = objectionMatch.Groups[1].Value.Trim();
            var validTarget = roles.Any(r => r.Name == targetRole);

            if (validTarget)
            {
                var nextRound = context.Round + 1;
                return CommandResult.OkAndContinueWith(
                    $"{role.DisplayName} objects, requesting response from {targetRole}",
                    $"SkillRoundCommand:{targetRole}:{nextRound}",
                    $"SkillRoundCommand:{context.SkillName}:{nextRound}",
                    "ConvergenceCheckCommand");
            }
        }

        // AGREE or SUGGESTION -> no insertion
        return CommandResult.Ok($"{role.DisplayName} (Round {context.Round}): contributed");
    }

    private async Task<string> CallSkillRoundLlmAsync(
        RoleSkillDefinition role,
        Ticket ticket,
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
            ## Ticket
            {ticket.Title}
            {ticket.Description}

            ## Project Context
            {projectContext ?? "Not available"}

            ## Domain Rules
            {domainRules ?? "Not available"}

            ## Code Map
            {codeMap ?? "Not available"}

            ## Discussion So Far
            {discussionSoFar}

            ## Your Task
            Based on the discussion so far, provide your perspective on this ticket.
            This is round {round}.

            If this is the first round for the lead role: Create an initial implementation plan.
            If responding to an existing plan: Review it from your perspective.

            At the end of your response, state clearly:
            - AGREE: if you accept the current plan
            - OBJECTION [target_role]: if you have a blocking concern for a specific role
            - SUGGESTION: if you have a non-blocking improvement

            Keep your contribution focused and concise (max 500 words).
            """;

        return await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
    }
}

/// <summary>
/// A single entry in the multi-role plan discussion log.
/// </summary>
public sealed record DiscussionEntry(
    string RoleName,
    string DisplayName,
    string Emoji,
    int Round,
    string Content);
