using System.Text.Json;
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
/// Analyzes ticket + project context via LLM to determine which specialist roles
/// should participate in planning. Inserts SkillRoundCommand entries and a
/// ConvergenceCheckCommand into the pipeline. Simple tickets skip discussion.
/// </summary>
public sealed class TriageHandler(
    ILlmClientFactory llmClientFactory,
    ILogger<TriageHandler> logger)
    : ICommandHandler<TriageContext>
{
    public async Task<CommandResult> ExecuteAsync(
        TriageContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null || roles.Count == 0)
        {
            logger.LogInformation("No available roles for triage, skipping discussion");
            return CommandResult.Ok("No roles available, skipping triage");
        }

        var ticket = context.Pipeline.Get<Ticket>(ContextKeys.Ticket);
        context.Pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);

        var llmClient = llmClientFactory.Create(context.AgentConfig);
        var triageResult = await CallTriageLlmAsync(
            ticket, roles, projectContext, llmClient, cancellationToken);

        if (triageResult is null || triageResult.Participants.Count == 0)
        {
            logger.LogInformation("Triage returned no participants, skipping discussion");
            return CommandResult.Ok("Triage: no roles needed, skipping discussion");
        }

        // Single participant -> skip discussion, continue with normal pipeline
        if (triageResult.Participants.Count == 1)
        {
            logger.LogInformation("Single role needed: {Role}, skipping discussion",
                triageResult.Participants[0]);
            return CommandResult.Ok(
                $"Triage: single role ({triageResult.Participants[0]}), no discussion needed");
        }

        // Multiple participants -> insert discussion commands
        var commandsToInsert = new List<string>();

        // Lead goes first
        commandsToInsert.Add($"SkillRoundCommand:{triageResult.Lead}:1");

        // Other participants follow
        foreach (var participant in triageResult.Participants
                     .Where(p => p != triageResult.Lead))
        {
            commandsToInsert.Add($"SkillRoundCommand:{participant}:1");
        }

        // Convergence check at the end
        commandsToInsert.Add("ConvergenceCheckCommand");

        logger.LogInformation(
            "Triage complete. Lead: {Lead}, Participants: {Participants}",
            triageResult.Lead,
            string.Join(", ", triageResult.Participants));

        return CommandResult.OkAndContinueWith(
            $"Triage complete. Lead: {triageResult.Lead}. " +
            $"Participants: {string.Join(", ", triageResult.Participants)}",
            commandsToInsert.ToArray());
    }

    private async Task<TriageResult?> CallTriageLlmAsync(
        Ticket ticket,
        IReadOnlyList<RoleSkillDefinition> roles,
        string? projectContext,
        ILlmClient llmClient,
        CancellationToken cancellationToken)
    {
        var rolesDescription = string.Join("\n", roles.Select(r =>
            $"- {r.Name}: {r.Description} (triggers: {string.Join(", ", r.Triggers)})"));

        var systemPrompt = """
            You are triaging a development ticket to determine which specialist roles
            should participate in planning. Respond with valid JSON only, no markdown.
            """;

        var userPrompt = $$"""
            ## Ticket
            {{ticket.Title}}
            {{ticket.Description}}

            ## Project Context
            {{projectContext ?? "Not available"}}

            ## Available Roles
            {{rolesDescription}}

            ## Instructions
            Analyze the ticket and determine:
            1. Which roles are needed (select from available roles only)
            2. Who should lead the discussion (creates the initial plan)

            Respond in JSON:
            {
              "lead": "role-name",
              "participants": ["role-name-1", "role-name-2"]
            }
            """;

        try
        {
            var response = await llmClient.CompleteAsync(
                systemPrompt, userPrompt, TaskType.Planning, cancellationToken);

            return ParseTriageResponse(response, roles);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Triage LLM call failed, skipping discussion");
            return null;
        }
    }

    private TriageResult? ParseTriageResponse(
        string response, IReadOnlyList<RoleSkillDefinition> availableRoles)
    {
        try
        {
            // Extract JSON from response (may be wrapped in markdown code blocks)
            var json = response;
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
                json = response[jsonStart..(jsonEnd + 1)];

            var parsed = JsonSerializer.Deserialize<TriageResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is null) return null;

            // Validate that all participants exist in available roles
            var validNames = availableRoles.Select(r => r.Name).ToHashSet();
            var validParticipants = parsed.Participants
                .Where(p => validNames.Contains(p))
                .ToList();

            if (validParticipants.Count == 0) return null;

            var lead = validNames.Contains(parsed.Lead) ? parsed.Lead : validParticipants[0];

            return new TriageResult { Lead = lead, Participants = validParticipants };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse triage response");
            return null;
        }
    }

    private sealed class TriageResult
    {
        public string Lead { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = [];
    }
}
